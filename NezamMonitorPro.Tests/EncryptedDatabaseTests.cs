using Microsoft.Data.Sqlite;
using NezamMonitorPro.Core.Data;
using NezamMonitorPro.Security.Db;

namespace NezamMonitorPro.Tests;

/// <summary>TEST-002 / SEC-001: the encrypted-database path. These tests run
/// the exact route that used to open a fresh plain connection (CreateBackup,
/// ImportAndroidChanges, the DatabaseService fallback) and fail with SQLite
/// error 26 "file is not a database". If these are green, that bug is gone.</summary>
public class EncryptedDatabaseTests
{
    private const string DbPassword = "TestDbPass123";

    [Fact]
    public void EncryptedDb_ReadWriteThroughNezamDatabase_Works()
    {
        using var tmp = new TestHelpers.TempDirectory();
        var dbPath = tmp.File("enc.db");

        using var enc = new EncryptedSqliteConnection(dbPath, DbPassword);
        using var db = new NezamDatabase(enc.Connection);

        // SQLCipher is genuinely active on this connection.
        using (var cmd = enc.Connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA cipher_version;";
            var version = cmd.ExecuteScalar()?.ToString();
            Assert.False(string.IsNullOrWhiteSpace(version));
        }

        var snapId = TestHelpers.SeedActiveSnapshot(db);
        db.SaveSetting(SettingKeys.Username, "31-4-0-04883");

        var cases = db.LoadCases(snapId);
        Assert.Single(cases);
        Assert.Equal("1404/514", cases[0].CaseNumber);
        Assert.Equal("31-4-0-04883", db.LoadSettings()[SettingKeys.Username]);
    }

    [Fact]
    public void EncryptedDb_WrongPassword_FailsToOpen()
    {
        using var tmp = new TestHelpers.TempDirectory();
        var dbPath = tmp.File("enc.db");

        using (var enc = new EncryptedSqliteConnection(dbPath, DbPassword))
        using (var db = new NezamDatabase(enc.Connection))
        {
            TestHelpers.SeedActiveSnapshot(db);
        }

        // Wrong password: opening (or the first read on) the database fails.
        // The failure surfaces either in the connection ctor (invalid
        // password) or in NezamDatabase.Initialize (error 26) — either way
        // it must throw, never silently open an empty database.
        Assert.ThrowsAny<Exception>(() =>
        {
            using var enc = new EncryptedSqliteConnection(dbPath, "WrongPassword");
            using var db = new NezamDatabase(enc.Connection);
            _ = db.LoadCases(1);
        });
    }

    [Fact]
    public void EncryptedDb_PlainConnection_FailsWithNotADatabase()
    {
        using var tmp = new TestHelpers.TempDirectory();
        var dbPath = tmp.File("enc.db");

        using (var enc = new EncryptedSqliteConnection(dbPath, DbPassword))
        using (var db = new NezamDatabase(enc.Connection))
        {
            TestHelpers.SeedActiveSnapshot(db);
        }

        // This is the old SEC-001 path: a connection without the key.
        SQLitePCL.Batteries_V2.Init();
        using var plain = new SqliteConnection($"Data Source={dbPath}");
        plain.Open();
        var ex = Assert.ThrowsAny<SqliteException>(() =>
        {
            using var cmd = plain.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master";
            _ = cmd.ExecuteScalar();
        });
        Assert.Contains("file is not a database", ex.Message);
    }

    [Fact]
    public void EncryptedDb_NezamDatabaseStringCtor_OnEncryptedFile_Throws()
    {
        using var tmp = new TestHelpers.TempDirectory();
        var dbPath = tmp.File("enc.db");

        using (var enc = new EncryptedSqliteConnection(dbPath, DbPassword))
        using (var db = new NezamDatabase(enc.Connection))
        {
            TestHelpers.SeedActiveSnapshot(db);
        }

        // The string ctor builds a plain connection: it must fail on an
        // encrypted file instead of returning an empty database.
        Assert.ThrowsAny<Exception>(() => new NezamDatabase(dbPath));
    }
}
