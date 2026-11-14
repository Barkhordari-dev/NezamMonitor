using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using NezamMonitorPro.Core.Data;

namespace NezamMonitorPro.Core.Backup;

/// <summary>
/// Creates encrypted backup of the entire application state.
/// Format: .nzbkp (ZIP with encrypted JSON)
/// Format v2: database bytes encrypted with AES-256-GCM (random salt +
/// random nonce per backup, both stored in the manifest); the manifest
/// checksum is SHA-256 over the encrypted bytes. Backups made by v1
/// (AES-CBC, fixed salt) cannot be restored by v2.
/// </summary>
public sealed class BackupEngine
{
    private const string FORMAT = "NezamMonitor.Backup";
    private const string VERSION = "2.0";

    // PBKDF2-SHA256 iterations for the backup key (OWASP 2023 recommendation)
    private const int Pbkdf2Iterations = 600_000;

    // JSON written by the other platform uses camelCase property names
    // ("format", "checksum", "cases", "caseNumber", ...). The default
    // deserializer is case-sensitive, so without this nothing binds:
    // a real camelCase manifest deserializes to an empty ImportManifest
    // with default options (verified with a scratch program).
    private static readonly JsonSerializerOptions CaseInsensitiveJsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Validate a package manifest checksum (W4): SHA-256 over the data file's
    /// bytes must equal manifest.checksum. Returns true when there is nothing
    /// to validate (no manifest, or no checksum in it) so older packages keep
    /// importing; returns false (and records the error) on a mismatch, and the
    /// caller must reject the import.
    /// </summary>
    private static bool ValidateManifestChecksum(string tempDir, string dataFileName, ImportResult result)
    {
        var manifestPath = Path.Combine(tempDir, "manifest.json");
        if (!File.Exists(manifestPath)) return true;
        var manifest = JsonSerializer.Deserialize<ImportManifest>(File.ReadAllText(manifestPath), CaseInsensitiveJsonOptions);
        if (manifest == null || string.IsNullOrEmpty(manifest.Checksum)) return true;
        var dataPath = Path.Combine(tempDir, dataFileName);
        if (!File.Exists(dataPath))
        {
            result.Errors.Add($"Checksum validation failed: missing {dataFileName}.");
            return false;
        }
        using var sha = SHA256.Create();
        var computed = Convert.ToHexString(sha.ComputeHash(File.ReadAllBytes(dataPath)));
        if (!string.Equals(computed, manifest.Checksum, StringComparison.OrdinalIgnoreCase))
        {
            result.Errors.Add("Checksum mismatch: the package data does not match its manifest. Import rejected.");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Create a complete backup of the application.
    /// Uses the already-open encrypted connection (conn) for the WAL
    /// checkpoint and the Settings read — never opens a fresh plain
    /// connection, which would fail on the encrypted database.
    /// Format v2: the database file bytes are encrypted with AES-256-GCM
    /// (random salt + random nonce per backup, both in the manifest);
    /// manifest.Checksum is SHA-256 over the encrypted bytes.
    /// </summary>
    public static BackupResult CreateBackup(SqliteConnection conn, string dbPath, string outputPath, string backupPassword)
    {
        var result = new BackupResult();
        try
        {
            if (conn == null) throw new ArgumentNullException(nameof(conn));
            if (string.IsNullOrEmpty(backupPassword))
            {
                result.Errors.Add("رمز بکاپ نمی‌تواند خالی باشد.");
                return result;
            }
            if (!File.Exists(dbPath))
            {
                result.Errors.Add("Database file not found.");
                return result;
            }

            // WAL checkpoint to flush pending writes and release locks
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "PRAGMA wal_checkpoint(FULL)";
                cmd.ExecuteScalar();
            }

            // Read database as bytes (safe after checkpoint)
            var dbBytes = File.ReadAllBytes(dbPath);

            // Read all settings through the open encrypted connection
            var settings = new Dictionary<string, string>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT Key, Value FROM Settings";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    settings[reader.GetString(0)] = reader.GetString(1);
                }
            }

            // Build backup data
            var backupData = new BackupData
            {
                Format = FORMAT,
                Version = VERSION,
                CreatedAt = DateTime.UtcNow.ToString("o"),
                DatabaseBytes = Convert.ToBase64String(dbBytes),
                Settings = settings,
                DbSizeBytes = dbBytes.Length
            };

            var jsonData = JsonSerializer.Serialize(backupData, new JsonSerializerOptions { WriteIndented = true });

            // Encrypt with password (AES-256-GCM, random salt + nonce per backup)
            var salt = RandomNumberGenerator.GetBytes(32);
            var nonce = RandomNumberGenerator.GetBytes(12);
            var encryptedBytes = EncryptBytes(Encoding.UTF8.GetBytes(jsonData), backupPassword, salt, nonce);

            // Compute checksum over the encrypted bytes
            using var sha = SHA256.Create();
            var checksum = Convert.ToHexString(sha.ComputeHash(encryptedBytes));

            // Create manifest
            var manifest = new BackupManifest
            {
                Format = FORMAT,
                Version = VERSION,
                CreatedAt = DateTime.UtcNow.ToString("o"),
                Encrypted = true,
                Checksum = checksum,
                DbSizeBytes = dbBytes.Length,
                SettingsCount = settings.Count,
                Salt = Convert.ToBase64String(salt),
                Nonce = Convert.ToBase64String(nonce)
            };

            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });

            // Write ZIP
            if (File.Exists(outputPath))
                File.Delete(outputPath);

            using (var zip = ZipFile.Open(outputPath, ZipArchiveMode.Create))
            {
                AddEntry(zip, "manifest.json", manifestJson);
                var dataEntry = zip.CreateEntry("data.enc", CompressionLevel.Optimal);
                using (var stream = dataEntry.Open())
                    stream.Write(encryptedBytes, 0, encryptedBytes.Length);
            }

            result.Success = true;
            result.OutputPath = outputPath;
            result.FileSize = new FileInfo(outputPath).Length;
            result.Manifest = manifest;
            return result;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Backup failed: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Restore from backup. File operations only — no database connection,
    /// so the caller must have closed the live connection first
    /// (DatabaseService.Close) before calling this, and reopen it after.
    /// Validates the manifest checksum over the encrypted bytes BEFORE
    /// decrypting, and rejects backups from the old v1 format.
    /// </summary>
    public static BackupResult RestoreBackup(string backupPath, string dbPath, string backupPassword)
    {
        var result = new BackupResult();
        try
        {
            if (!File.Exists(backupPath))
            {
                result.Errors.Add("Backup file not found.");
                return result;
            }

            using var zip = ZipFile.OpenRead(backupPath);

            // Read manifest
            var manifestEntry = zip.GetEntry("manifest.json");
            if (manifestEntry == null)
            {
                result.Errors.Add("Invalid backup: missing manifest.json");
                return result;
            }

            BackupManifest? manifest;
            using (var reader = new StreamReader(manifestEntry.Open()))
            {
                manifest = JsonSerializer.Deserialize<BackupManifest>(reader.ReadToEnd());
                if (manifest == null || manifest.Format != FORMAT)
                {
                    result.Errors.Add("Invalid backup format");
                    return result;
                }
                if (manifest.Version != VERSION)
                {
                    result.Errors.Add("نسخه این بکاپ پشتیبانی نمی‌شود؛ با نسخه قدیمی برنامه ساخته شده است.");
                    return result;
                }
            }

            // Read encrypted data
            var dataEntry = zip.GetEntry("data.enc");
            if (dataEntry == null)
            {
                result.Errors.Add("Invalid backup: missing data.enc");
                return result;
            }

            byte[] encryptedBytes;
            using (var stream = dataEntry.Open())
            using (var ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                encryptedBytes = ms.ToArray();
            }

            // Verify checksum FIRST (over the encrypted bytes, against the manifest)
            using var sha = SHA256.Create();
            var computedChecksum = Convert.ToHexString(sha.ComputeHash(encryptedBytes));
            if (!string.Equals(computedChecksum, manifest.Checksum, StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add("Backup integrity check failed.");
                return result;
            }

            // Decrypt (GCM authentication fails on a wrong password)
            byte[] plainBytes;
            try
            {
                var salt = Convert.FromBase64String(manifest.Salt);
                var nonce = Convert.FromBase64String(manifest.Nonce);
                plainBytes = DecryptBytes(encryptedBytes, backupPassword, salt, nonce);
            }
            catch (CryptographicException)
            {
                result.Errors.Add("Failed to decrypt backup. Wrong password?");
                return result;
            }

            // Parse
            var backupData = JsonSerializer.Deserialize<BackupData>(Encoding.UTF8.GetString(plainBytes));
            if (backupData == null || string.IsNullOrEmpty(backupData.DatabaseBytes))
            {
                result.Errors.Add("Failed to decrypt backup. Wrong password?");
                return result;
            }

            // Restore database
            var dbBytes = Convert.FromBase64String(backupData.DatabaseBytes);
            var dbDir = Path.GetDirectoryName(dbPath);
            if (dbDir != null) Directory.CreateDirectory(dbDir);
            
            // Backup current DB first
            if (File.Exists(dbPath))
            {
                var tempPath = dbPath + ".pre-restore";
                File.Copy(dbPath, tempPath, true);
            }

            File.WriteAllBytes(dbPath, dbBytes);

            result.Success = true;
            result.RecordCount = (int)backupData.DbSizeBytes;
            return result;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Restore failed: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Reset to factory settings (clear all data, keep templates).
    /// Uses the already-open encrypted connection — never opens a fresh
    /// plain connection, which would fail on the encrypted database.
    /// </summary>
    public static BackupResult ResetToFactory(SqliteConnection conn)
    {
        var result = new BackupResult();
        try
        {
            if (conn == null) throw new ArgumentNullException(nameof(conn));

            using var transaction = conn.BeginTransaction();

            void ExecuteInTransaction(string sql)
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }

            // اول فرزندها، بعد والدها (foreign_keys روشن است)
            ExecuteInTransaction("DELETE FROM Specifications");
            ExecuteInTransaction("DELETE FROM Engineers");
            ExecuteInTransaction("DELETE FROM Fees");
            ExecuteInTransaction("DELETE FROM Reports");
            ExecuteInTransaction("DELETE FROM FollowUpEdits");
            ExecuteInTransaction("DELETE FROM Changes");
            ExecuteInTransaction("DELETE FROM Cases");
            ExecuteInTransaction("DELETE FROM Snapshots");
            ExecuteInTransaction("DELETE FROM Settings");

            transaction.Commit();

            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Reset failed: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Import changes from Android export (.nzmdata package).
    /// </summary>
    /// <summary>
    /// Parses an edit timestamp that may be ISO-8601 (Android, Gregorian) or
    /// "yyyy/MM/dd HH:mm" (Windows under fa-IR, Jalali). Returns the instant,
    /// or null when the text cannot be parsed (fail-open: the edit applies).
    /// The year decides the calendar: &gt;= 1600 is Gregorian, below is Jalali.
    /// </summary>
    private static DateTimeOffset? ParseEditTimestamp(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var s = text.Trim();
        if (s.Contains('T') || s.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
        {
            if (DateTimeOffset.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var dto))
                return dto;
            return null;
        }
        var m = System.Text.RegularExpressions.Regex.Match(s,
            @"^(\d{4})[/-](\d{1,2})[/-](\d{1,2})[ ](\d{1,2}):(\d{2})(?::(\d{2}))?$");
        if (!m.Success) return null;
        var year = int.Parse(m.Groups[1].Value);
        var month = int.Parse(m.Groups[2].Value);
        var day = int.Parse(m.Groups[3].Value);
        var hour = int.Parse(m.Groups[4].Value);
        var minute = int.Parse(m.Groups[5].Value);
        var second = m.Groups[6].Success ? int.Parse(m.Groups[6].Value) : 0;
        DateTime gregorian;
        try
        {
            gregorian = year >= 1600
                ? new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified)
                : new DateTime(year, month, day, hour, minute, second, new System.Globalization.PersianCalendar());
        }
        catch { return null; }
        return new DateTimeOffset(gregorian, TimeZoneInfo.Local.GetUtcOffset(gregorian));
    }

    /// <summary>Formats an instant as a Jalali "yyyy/MM/dd HH:mm" wall-clock
    /// (the format SaveFollowUpEdit writes), so the column keeps one format.</summary>
    private static string ToJalaliTimestamp(DateTimeOffset instant)
    {
        var local = instant.LocalDateTime;
        var pc = new System.Globalization.PersianCalendar();
        return $"{pc.GetYear(local):0000}/{pc.GetMonth(local):00}/{pc.GetDayOfMonth(local):00} {local.Hour:00}:{local.Minute:00}";
    }

    /// <summary>
    /// Last-writer-wins (§6): a file's edit wins only when its timestamp is
    /// strictly newer than the local row's UpdatedAt. On a tie Windows wins
    /// (the local row is kept). Unparseable/missing timestamps fail open.
    /// </summary>
    private static bool FileEditWins(string? fileTimestamp, string? localUpdatedAt)
    {
        var file = ParseEditTimestamp(fileTimestamp);
        var local = ParseEditTimestamp(localUpdatedAt);
        if (file == null || local == null) return true;
        return file.Value > local.Value;
    }

    /// <summary>Winner's instant as a Jalali wall-clock; falls back to now
    /// when the file's timestamp is missing or unparseable.</summary>
    private static string WinnerTimestamp(string? fileTimestamp)
    {
        var parsed = ParseEditTimestamp(fileTimestamp);
        return parsed is { } p ? ToJalaliTimestamp(p) : DateTime.Now.ToString("yyyy/MM/dd HH:mm");
    }

    /// <summary>
    /// Import changes from Android export (.nzmdata package).
    /// Uses the already-open encrypted connection (conn) — never opens a
    /// fresh plain connection, which would fail on the encrypted database.
    /// The connection is NOT closed or disposed here; it belongs to the caller.
    /// When the package is encrypted (SEC-015), <paramref name="password"/>
    /// must be the export password; a wrong or missing password is reported
    /// as a Persian error in the result (no raw exception to the user).
    /// </summary>
    public static ImportResult ImportAndroidChanges(string packagePath, SqliteConnection conn, string? password = null)
    {
        var result = new ImportResult();
        try
        {
            if (conn == null) throw new ArgumentNullException(nameof(conn));
            if (!File.Exists(packagePath))
            {
                result.Errors.Add("Package file not found.");
                return result;
            }

            var tempDir = Path.Combine(Path.GetTempPath(), $"nezam_import_{Guid.NewGuid():N}");
            try
            {
                ZipFile.ExtractToDirectory(packagePath, tempDir);

                // Support both formats:
                // 1. changes.json (NezamMonitor.AndroidChanges) - direct change set
                // 2. manifest.json + data.json (NezamMonitor.AndroidData) - full dataset
                var changesPath = Path.Combine(tempDir, "changes.json");
                var manifestPath = Path.Combine(tempDir, "manifest.json");

                if (File.Exists(changesPath))
                {
                    // Format 1: Android Change Set
                    var changesJson = File.ReadAllText(changesPath);
                    var changeSet = JsonSerializer.Deserialize<AndroidChangeSetDto>(changesJson, CaseInsensitiveJsonOptions);
                    if (changeSet == null || changeSet.Format != "NezamMonitor.AndroidChanges")
                    {
                        result.Errors.Add("Invalid change set format");
                        return result;
                    }

                    // W4: validate the manifest checksum when the package ships one.
                    if (!ValidateManifestChecksum(tempDir, "changes.json", result))
                        return result;

                    var appliedCount = 0;
                    var skippedCount = 0;
                    foreach (var change in changeSet.Changes)
                    {
                        if (change.Entity == "FollowUp" && change.Field == "description")
                        {
                            // V1/§6: last writer wins by timestamp; on a tie
                            // Windows wins (the local row is kept).
                            string? localUpdatedAt = null;
                            using (var q = conn.CreateCommand())
                            {
                                q.CommandText = "SELECT UpdatedAt FROM FollowUpEdits WHERE CaseNumber=@cn";
                                q.Parameters.AddWithValue("@cn", change.RecordId);
                                localUpdatedAt = q.ExecuteScalar() as string;
                            }
                            if (!FileEditWins(change.ModifiedAt, localUpdatedAt))
                            {
                                skippedCount++;
                                continue;
                            }

                            using var cmd = conn.CreateCommand();
                            // changes.json carries only the description: the other four
                            // follow-up fields must survive (they were written by the
                            // full-dataset import or by Windows itself). INSERT OR REPLACE
                            // with '' would wipe them on every Android description edit.
                            cmd.CommandText = @"INSERT OR REPLACE INTO FollowUpEdits 
                                (CaseNumber, Description, CaseNumberEdit, OwnerEdit, AddressEdit, OwnerMobileEdit, CreatedAt, UpdatedAt) 
                                VALUES (@cn, @desc,
                                    COALESCE((SELECT CaseNumberEdit FROM FollowUpEdits WHERE CaseNumber=@cn), ''),
                                    COALESCE((SELECT OwnerEdit FROM FollowUpEdits WHERE CaseNumber=@cn), ''),
                                    COALESCE((SELECT AddressEdit FROM FollowUpEdits WHERE CaseNumber=@cn), ''),
                                    COALESCE((SELECT OwnerMobileEdit FROM FollowUpEdits WHERE CaseNumber=@cn), ''),
                                    COALESCE((SELECT CreatedAt FROM FollowUpEdits WHERE CaseNumber=@cn), @now), @upd)";
                            cmd.Parameters.AddWithValue("@cn", change.RecordId);
                            cmd.Parameters.AddWithValue("@desc", change.NewValue ?? "");
                            cmd.Parameters.AddWithValue("@now", DateTime.Now.ToString("yyyy/MM/dd HH:mm"));
                            cmd.Parameters.AddWithValue("@upd", WinnerTimestamp(change.ModifiedAt));
                            cmd.ExecuteNonQuery();
                            appliedCount++;
                        }
                    }

                    // Hidden menu: permanent overrides win over the imported values.
                    try { HiddenOverrideApplier.ApplyAll(conn); } catch { }

                    result.Success = true;
                    result.AppliedChanges = appliedCount;
                    result.Message = $"Applied {appliedCount} changes from Android (v{changeSet.Version})" +
                        (skippedCount > 0 ? $" ({skippedCount} skipped: older than the local edit)" : "");
                    return result;
                }
                else if (File.Exists(manifestPath))
                {
                    // Format 2: Full dataset (existing logic)
                    var manifest = JsonSerializer.Deserialize<ImportManifest>(File.ReadAllText(manifestPath), CaseInsensitiveJsonOptions);
                    if (manifest == null || manifest.Format != "NezamMonitor.AndroidData")
                    {
                        result.Errors.Add("Invalid package format");
                        return result;
                    }

                    // SEC-015: an encrypted package needs its password before
                    // anything else is read from it.
                    if (manifest.Encrypted && string.IsNullOrEmpty(password))
                    {
                        result.Errors.Add("این بسته رمزنگاری شده است؛ رمز آن را وارد کن.");
                        return result;
                    }

                    var dataPath = Path.Combine(tempDir, "data.json");
                    if (!File.Exists(dataPath))
                    {
                        result.Errors.Add("Missing data.json");
                        return result;
                    }

                    // W4: SHA-256 over data.json must match manifest.checksum.
                    // For an encrypted package the stored bytes are the
                    // ciphertext, and the manifest checksum is over them.
                    if (!ValidateManifestChecksum(tempDir, "data.json", result))
                        return result;

                    string dataJson;
                    if (manifest.Encrypted)
                    {
                        try
                        {
                            var salt = Convert.FromBase64String(manifest.Salt);
                            var nonce = Convert.FromBase64String(manifest.Nonce);
                            var plainBytes = DecryptBytes(File.ReadAllBytes(dataPath), password!, salt, nonce);
                            dataJson = Encoding.UTF8.GetString(plainBytes);
                        }
                        catch (CryptographicException)
                        {
                            result.Errors.Add("رمز اشتباه است.");
                            return result;
                        }
                        catch (Exception ex) when (ex is FormatException or InvalidOperationException)
                        {
                            result.Errors.Add("بسته رمزنگاری‌شده خراب است.");
                            return result;
                        }
                    }
                    else
                    {
                        dataJson = File.ReadAllText(dataPath);
                    }
                    var data = JsonSerializer.Deserialize<ImportData>(dataJson, CaseInsensitiveJsonOptions);
                    if (data == null || data.Cases == null)
                    {
                        result.Errors.Add("Invalid data format");
                        return result;
                    }

                    var appliedCount = 0;
                    var skippedCount = 0;
                    foreach (var importCase in data.Cases)
                    {
                        // V3: the archive flag is OR — a file that says archived
                        // sets the flag; nothing in a file ever clears it. The
                        // flag follows the case across snapshots (no SnapshotId
                        // filter on purpose). A missing column (old DB) must not
                        // break the follow-up import below.
                        try
                        {
                            if (importCase.Archived)
                            {
                                using var flagCmd = conn.CreateCommand();
                                flagCmd.CommandText = "UPDATE Cases SET Archived=1 WHERE CaseNumber=@cn";
                                flagCmd.Parameters.AddWithValue("@cn", importCase.CaseNumber);
                                flagCmd.ExecuteNonQuery();
                            }
                            // V3: backfill the سامانه id when the local row lacks it.
                            if (!string.IsNullOrEmpty(importCase.DbId))
                            {
                                using var idCmd = conn.CreateCommand();
                                idCmd.CommandText = "UPDATE Cases SET ApiDbId=@dbid WHERE CaseNumber=@cn AND (ApiDbId IS NULL OR ApiDbId='')";
                                idCmd.Parameters.AddWithValue("@cn", importCase.CaseNumber);
                                idCmd.Parameters.AddWithValue("@dbid", importCase.DbId);
                                idCmd.ExecuteNonQuery();
                            }
                        }
                        catch { /* old DB without the new columns */ }

                        if (importCase.FollowUpEdit != null)
                        {
                            // V1/§6: last writer wins by updatedAt; on a tie
                            // Windows wins (the local row is kept).
                            string? localUpdatedAt = null;
                            using (var q = conn.CreateCommand())
                            {
                                q.CommandText = "SELECT UpdatedAt FROM FollowUpEdits WHERE CaseNumber=@cn";
                                q.Parameters.AddWithValue("@cn", importCase.CaseNumber);
                                localUpdatedAt = q.ExecuteScalar() as string;
                            }
                            var fileEdit = importCase.FollowUpEdit;
                            if (!FileEditWins(fileEdit.UpdatedAt, localUpdatedAt))
                            {
                                skippedCount++;
                                continue;
                            }

                            using var cmd = conn.CreateCommand();
                            cmd.CommandText = @"INSERT OR REPLACE INTO FollowUpEdits 
                                (CaseNumber, Description, CaseNumberEdit, OwnerEdit, AddressEdit, OwnerMobileEdit, CreatedAt, UpdatedAt) 
                                VALUES (@cn, @desc, @cnEdit, @ownerEdit, @addrEdit, @mobileEdit, 
                                    COALESCE((SELECT CreatedAt FROM FollowUpEdits WHERE CaseNumber=@cn), NULLIF(@fc, ''), @now), @upd)";
                            cmd.Parameters.AddWithValue("@cn", importCase.CaseNumber);
                            cmd.Parameters.AddWithValue("@desc", fileEdit.Description ?? "");
                            cmd.Parameters.AddWithValue("@cnEdit", fileEdit.CaseNumberEdit ?? "");
                            cmd.Parameters.AddWithValue("@ownerEdit", fileEdit.OwnerEdit ?? "");
                            cmd.Parameters.AddWithValue("@addrEdit", fileEdit.AddressEdit ?? "");
                            cmd.Parameters.AddWithValue("@mobileEdit", fileEdit.OwnerMobileEdit ?? "");
                            cmd.Parameters.AddWithValue("@now", DateTime.Now.ToString("yyyy/MM/dd HH:mm"));
                            cmd.Parameters.AddWithValue("@fc", ParseEditTimestamp(fileEdit.CreatedAt) is { } created
                                ? ToJalaliTimestamp(created) : "");
                            cmd.Parameters.AddWithValue("@upd", WinnerTimestamp(fileEdit.UpdatedAt));
                            cmd.ExecuteNonQuery();
                            appliedCount++;
                        }
                    }

                    // Hidden menu: permanent overrides win over the imported values.
                    try { HiddenOverrideApplier.ApplyAll(conn); } catch { }

                    result.Success = true;
                    result.AppliedChanges = appliedCount;
                    result.Message = $"Applied {appliedCount} changes from Android (full dataset)" +
                        (skippedCount > 0 ? $" ({skippedCount} skipped: older than the local edit)" : "");
                    return result;
                }
                else
                {
                    result.Errors.Add("No valid change file found (expected changes.json or manifest.json)");
                    return result;
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Import failed: {ex.Message}");
            return result;
        }
    }

    private static void AddEntry(ZipArchive zip, string entryName, string content)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    /// <summary>
    /// SEC-015: true when the package's manifest says its data.json entry
    /// is encrypted. Only reads the manifest; never throws (any problem
    /// means "not an encrypted package we can tell about").
    /// </summary>
    public static bool IsPackageEncrypted(string packagePath)
    {
        try
        {
            if (!File.Exists(packagePath)) return false;
            using var zip = ZipFile.OpenRead(packagePath);
            var entry = zip.GetEntry("manifest.json");
            if (entry == null) return false;
            using var reader = new StreamReader(entry.Open());
            var manifest = JsonSerializer.Deserialize<ImportManifest>(reader.ReadToEnd(), CaseInsensitiveJsonOptions);
            return manifest?.Encrypted == true;
        }
        catch { return false; }
    }

    // AES-256-GCM: کلید از PBKDF2-SHA256 (رمز + نمک تصادفی) ساخته می‌شود؛
    // خروجی = متن رمزنگاری‌شده + تگ ۱۶ بایتی (همان چیدمان AesGcmEncryption)
    private static byte[] EncryptBytes(byte[] plainBytes, string password, byte[] salt, byte[] nonce)
    {
        // .NET 10: Rfc2898DeriveBytes constructors are obsolete (SYSLIB0060);
        // the static Pbkdf2 method derives the identical key.
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, 32);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[16];
        using var aesGcm = new AesGcm(key, 16);
        aesGcm.Encrypt(nonce, plainBytes, cipher, tag);
        var result = new byte[cipher.Length + tag.Length];
        Buffer.BlockCopy(cipher, 0, result, 0, cipher.Length);
        Buffer.BlockCopy(tag, 0, result, cipher.Length, tag.Length);
        return result;
    }

    private static byte[] DecryptBytes(byte[] cipherWithTag, string password, byte[] salt, byte[] nonce)
    {
        if (cipherWithTag.Length < 16)
            throw new InvalidOperationException("داده رمزنگاری‌شده خراب است.");
        // .NET 10: Rfc2898DeriveBytes constructors are obsolete (SYSLIB0060);
        // the static Pbkdf2 method derives the identical key.
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, 32);
        var cipher = new byte[cipherWithTag.Length - 16];
        var tag = new byte[16];
        Buffer.BlockCopy(cipherWithTag, 0, cipher, 0, cipher.Length);
        Buffer.BlockCopy(cipherWithTag, cipher.Length, tag, 0, 16);
        var plain = new byte[cipher.Length];
        using var aesGcm = new AesGcm(key, 16);
        aesGcm.Decrypt(nonce, cipher, tag, plain);
        return plain;
    }
}

// ========== Models ==========

public class BackupData
{
    public string Format { get; set; } = "";
    public string Version { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string DatabaseBytes { get; set; } = "";
    public Dictionary<string, string> Settings { get; set; } = new();
    public long DbSizeBytes { get; set; }
    public string Checksum { get; set; } = "";
}

public class BackupManifest
{
    public string Format { get; set; } = "";
    public string Version { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public bool Encrypted { get; set; }
    public string Checksum { get; set; } = "";
    public long DbSizeBytes { get; set; }
    public int SettingsCount { get; set; }
    // نمک و nonce تصادفی هر بکاپ (Base64) — برای ساخت کلید و رمزگشایی لازم‌اند
    public string Salt { get; set; } = "";
    public string Nonce { get; set; } = "";
}

public class BackupResult
{
    public bool Success { get; set; }
    public string OutputPath { get; set; } = "";
    public long FileSize { get; set; }
    public BackupManifest? Manifest { get; set; }
    public List<string> Errors { get; set; } = new();
    public int RecordCount { get; set; }
}

public class ImportResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public int AppliedChanges { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class ImportManifest
{
    public string Format { get; set; } = "";
    public string Version { get; set; } = "";
    public string Checksum { get; set; } = "";
    /// <summary>SEC-015: true when the stored data.json entry is encrypted.</summary>
    public bool Encrypted { get; set; }
    /// <summary>Encryption algorithm id, e.g. "AES-256-GCM".</summary>
    public string Encryption { get; set; } = "";
    /// <summary>Random per-export salt (Base64) for the PBKDF2 key.</summary>
    public string Salt { get; set; } = "";
    /// <summary>Random per-export nonce (Base64) for AES-GCM.</summary>
    public string Nonce { get; set; } = "";
}

public class ImportData
{
    public List<ImportCase> Cases { get; set; } = new();
}

public class ImportCase
{
    public string CaseNumber { get; set; } = "";
    public bool Archived { get; set; }
    public string DbId { get; set; } = "";
    public ImportFollowUpEdit? FollowUpEdit { get; set; }
}

public class ImportFollowUpEdit
{
    public string Description { get; set; } = "";
    public string CaseNumberEdit { get; set; } = "";
    public string OwnerEdit { get; set; } = "";
    public string AddressEdit { get; set; } = "";
    public string OwnerMobileEdit { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
}

// ========== Android Change Set DTOs ==========

public class AndroidChangeSetDto
{
    public string Format { get; set; } = "";
    public int Version { get; set; } = 1;
    public string Source { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public List<AndroidChangeDto> Changes { get; set; } = new();
}

public class AndroidChangeDto
{
    public string Entity { get; set; } = "";
    public string RecordId { get; set; } = "";
    public string Field { get; set; } = "";
    public string OldValue { get; set; } = "";
    public string NewValue { get; set; } = "";
    public string ModifiedAt { get; set; } = "";
}
