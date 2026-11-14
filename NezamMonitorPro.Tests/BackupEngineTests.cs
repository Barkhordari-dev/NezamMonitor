using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using NezamMonitorPro.Core.Backup;
using NezamMonitorPro.Core.Data;
using NezamMonitorPro.Core.Export;
using NezamMonitorPro.Security.Db;

namespace NezamMonitorPro.Tests;

/// <summary>TEST-002: BackupEngine + the encrypted paths (SEC-001, SEC-003,
/// SEC-015). Every test uses a real encrypted database where the production
/// code demands one — that is exactly the path that used to open a plain
/// connection and fail with SQLite error 26.</summary>
public class BackupEngineTests
{
    private const string DbPassword = "TestDbPass123";
    private const string BackupPassword = "BackupPass123";
    private const string ExportPassword = "ExportPass123";

    private static NezamDatabase PlainDb(TestHelpers.TempDirectory tmp, string name = "test.db")
    {
        var db = new NezamDatabase(tmp.File(name));
        return db;
    }

    private static void WritePackage(string dir, string manifestJson, byte[] dataBytes)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "manifest.json"), manifestJson);
        File.WriteAllBytes(Path.Combine(dir, "data.json"), dataBytes);
    }

    private static string ManifestJson(string checksum) =>
        JsonSerializer.Serialize(new
        {
            format = "NezamMonitor.AndroidData",
            version = "1.0",
            formatVersion = "1",
            checksum
        });

    [Fact]
    public void Import_WrongChecksum_IsRejected()
    {
        using var tmp = new TestHelpers.TempDirectory();
        var dataBytes = Encoding.UTF8.GetBytes("{\"cases\":[]}");
        var pkgDir = tmp.File("pkg-wrong");
        WritePackage(pkgDir, ManifestJson(new string('0', 64)), dataBytes);
        var packagePath = tmp.File("wrong.nzmdata");
        TestHelpers.BuildPackage(pkgDir, packagePath);

        using var db = PlainDb(tmp);
        var result = BackupEngine.ImportAndroidChanges(packagePath, db.Connection);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("Checksum mismatch"));
    }

    [Fact]
    public void Import_CorrectChecksum_IsAccepted()
    {
        using var tmp = new TestHelpers.TempDirectory();
        var dataBytes = Encoding.UTF8.GetBytes("{\"cases\":[]}");
        var pkgDir = tmp.File("pkg-ok");
        WritePackage(pkgDir, ManifestJson(TestHelpers.Sha256Hex(dataBytes)), dataBytes);
        var packagePath = tmp.File("ok.nzmdata");
        TestHelpers.BuildPackage(pkgDir, packagePath);

        using var db = PlainDb(tmp);
        var result = BackupEngine.ImportAndroidChanges(packagePath, db.Connection);

        Assert.True(result.Success, string.Join("; ", result.Errors));
        Assert.Equal(0, result.AppliedChanges);
    }

    [Fact]
    public void Import_RealSample_Applies75FollowUpEdits()
    {
        // The sample is a local fixture, not part of the repo: runs only
        // where it exists (this VM and the developer's machine).
        var sampleDir = TestHelpers.FindSampleDir();
        if (sampleDir == null) return;

        using var tmp = new TestHelpers.TempDirectory();
        var packagePath = tmp.File("sample.nzmdata");
        TestHelpers.BuildPackage(sampleDir, packagePath);

        Assert.False(BackupEngine.IsPackageEncrypted(packagePath));

        using var db = PlainDb(tmp);
        var result = BackupEngine.ImportAndroidChanges(packagePath, db.Connection);

        Assert.True(result.Success, string.Join("; ", result.Errors));
        // 77 cases in the sample, 75 carry a followUpEdit; the other two
        // have none, so exactly 75 edits apply (verified 2026-11-12).
        Assert.Equal(75, result.AppliedChanges);

        // Spot-check one imported edit actually landed in the database.
        var edits = db.GetAllFollowUpEdits();
        Assert.True(edits.Count >= 75);
        Assert.True(edits.ContainsKey("1404/514"));
    }

    [Fact]
    public void BackupV2_OnEncryptedDb_RoundTrips_WithRightPassword()
    {
        using var tmp = new TestHelpers.TempDirectory();
        var dbPath = tmp.File("enc.db");
        var backupPath = tmp.File("backup.nzbkp");
        var restoredPath = tmp.File("restored.db");
        long snapId;

        using (var enc = new EncryptedSqliteConnection(dbPath, DbPassword))
        using (var db = new NezamDatabase(enc.Connection))
        {
            snapId = TestHelpers.SeedActiveSnapshot(db);
            db.SaveSetting(SettingKeys.Username, "31-4-0-04883");

            var backup = BackupEngine.CreateBackup(enc.Connection, dbPath, backupPath, BackupPassword);
            Assert.True(backup.Success, string.Join("; ", backup.Errors));
            Assert.True(File.Exists(backupPath));
            Assert.Equal("2.0", backup.Manifest?.Version);
        }

        var restore = BackupEngine.RestoreBackup(backupPath, restoredPath, BackupPassword);
        Assert.True(restore.Success, string.Join("; ", restore.Errors));

        // The restored file is still the same encrypted database: it opens
        // with the database password and the seeded case is there.
        using (var enc2 = new EncryptedSqliteConnection(restoredPath, DbPassword))
        using (var db2 = new NezamDatabase(enc2.Connection))
        {
            var cases = db2.LoadCases(snapId);
            Assert.Single(cases);
            Assert.Equal("1404/514", cases[0].CaseNumber);
            Assert.Equal("31-4-0-04883", db2.LoadSettings()[SettingKeys.Username]);
        }
    }

    [Fact]
    public void BackupV2_Restore_WrongPassword_Fails()
    {
        using var tmp = new TestHelpers.TempDirectory();
        var dbPath = tmp.File("enc.db");
        var backupPath = tmp.File("backup.nzbkp");

        using (var enc = new EncryptedSqliteConnection(dbPath, DbPassword))
        using (var db = new NezamDatabase(enc.Connection))
        {
            TestHelpers.SeedActiveSnapshot(db);
            var backup = BackupEngine.CreateBackup(enc.Connection, dbPath, backupPath, BackupPassword);
            Assert.True(backup.Success, string.Join("; ", backup.Errors));
        }

        var restore = BackupEngine.RestoreBackup(backupPath, tmp.File("restored.db"), "WrongPassword");
        Assert.False(restore.Success);
        Assert.Contains(restore.Errors, e => e.Contains("Wrong password"));
    }

    [Fact]
    public void EncryptedPackage_NeedsPassword_WrongPasswordFails_RightPasswordImports()
    {
        using var tmp = new TestHelpers.TempDirectory();

        // Source: one case + one follow-up edit, exported with a password.
        string packagePath;
        using (var srcDb = PlainDb(tmp, "src.db"))
        {
            TestHelpers.SeedActiveSnapshot(srcDb);
            using (var cmd = srcDb.Connection.CreateCommand())
            {
                cmd.CommandText = @"INSERT INTO FollowUpEdits
                    (CaseNumber, Description, CaseNumberEdit, OwnerEdit, AddressEdit, OwnerMobileEdit, CreatedAt, UpdatedAt)
                    VALUES ('1404/514', 'توضیح تستی', '', '', '', '', '2026-11-13 10:00', '2026-11-13T10:00:00Z')";
                cmd.ExecuteNonQuery();
            }

            packagePath = tmp.File("enc.nzmdata");
            var export = new AndroidExportEngine(srcDb).Export(packagePath, "1.0", ExportPassword);
            Assert.True(export.Success, string.Join("; ", export.Errors));
        }

        Assert.True(BackupEngine.IsPackageEncrypted(packagePath));

        // No password: the import asks for one.
        using (var db = PlainDb(tmp, "dst1.db"))
        {
            var noPass = BackupEngine.ImportAndroidChanges(packagePath, db.Connection);
            Assert.False(noPass.Success);
            Assert.Contains(noPass.Errors, e => e.Contains("رمز آن را وارد کن"));
        }

        // Wrong password: rejected as wrong password.
        using (var db = PlainDb(tmp, "dst2.db"))
        {
            var wrong = BackupEngine.ImportAndroidChanges(packagePath, db.Connection, "Nope123");
            Assert.False(wrong.Success);
            Assert.Contains(wrong.Errors, e => e.Contains("رمز اشتباه است"));
        }

        // Right password: the follow-up edit lands.
        using (var db = PlainDb(tmp, "dst3.db"))
        {
            var ok = BackupEngine.ImportAndroidChanges(packagePath, db.Connection, ExportPassword);
            Assert.True(ok.Success, string.Join("; ", ok.Errors));
            Assert.Equal(1, ok.AppliedChanges);
            var edits = db.GetAllFollowUpEdits();
            Assert.True(edits.ContainsKey("1404/514"));
            Assert.Equal("توضیح تستی", edits["1404/514"].Description);
        }
    }
}
