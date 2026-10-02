using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NezamMonitor.Core.Backup;

/// <summary>
/// Creates encrypted backup of the entire application state.
/// Format: .nzbkp (ZIP with encrypted JSON)
/// </summary>
public sealed class BackupEngine
{
    private const string FORMAT = "NezamMonitor.Backup";
    private const string VERSION = "1.0";

    /// <summary>
    /// Create a complete backup of the application.
    /// </summary>
    public static BackupResult CreateBackup(string dbPath, string outputPath, string backupPassword)
    {
        var result = new BackupResult();
        try
        {
            if (!File.Exists(dbPath))
            {
                result.Errors.Add("Database file not found.");
                return result;
            }

            // WAL checkpoint to flush pending writes and release locks
            using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA wal_checkpoint(FULL)";
                cmd.ExecuteScalar();
            }

            // Read database as bytes (safe after checkpoint)
            var dbBytes = File.ReadAllBytes(dbPath);

            // Read all settings
            var settings = new Dictionary<string, string>();
            using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
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

            // Encrypt with password
            var encryptedBytes = EncryptString(jsonData, backupPassword);

            // Compute checksum
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
                SettingsCount = settings.Count
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
    /// Restore from backup.
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

            using (var reader = new StreamReader(manifestEntry.Open()))
            {
                var manifest = JsonSerializer.Deserialize<BackupManifest>(reader.ReadToEnd());
                if (manifest == null || manifest.Format != FORMAT)
                {
                    result.Errors.Add("Invalid backup format");
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

            // Decrypt
            var jsonData = DecryptString(encryptedBytes, backupPassword);

            // Parse
            var backupData = JsonSerializer.Deserialize<BackupData>(jsonData);
            if (backupData == null)
            {
                result.Errors.Add("Failed to decrypt backup. Wrong password?");
                return result;
            }

            // Verify checksum
            using var sha = SHA256.Create();
            var computedChecksum = Convert.ToHexString(sha.ComputeHash(encryptedBytes));
            if (computedChecksum != backupData.Checksum)
            {
                result.Errors.Add("Backup integrity check failed.");
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
    /// </summary>
    public static BackupResult ResetToFactory(string dbPath)
    {
        var result = new BackupResult();
        try
        {
            if (!File.Exists(dbPath))
            {
                result.Errors.Add("Database file not found.");
                return result;
            }

            using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}");
            conn.Open();
            using var transaction = conn.BeginTransaction();

            void ExecuteInTransaction(string sql)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }

            ExecuteInTransaction("DELETE FROM Cases");
            ExecuteInTransaction("DELETE FROM Specifications");
            ExecuteInTransaction("DELETE FROM Engineers");
            ExecuteInTransaction("DELETE FROM Fees");
            ExecuteInTransaction("DELETE FROM Reports");
            ExecuteInTransaction("DELETE FROM FollowUpEdits");
            ExecuteInTransaction("DELETE FROM Changes");
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
    public static ImportResult ImportAndroidChanges(string packagePath, string dbPath)
    {
        var result = new ImportResult();
        try
        {
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
                    var changeSet = JsonSerializer.Deserialize<AndroidChangeSetDto>(changesJson);
                    if (changeSet == null || changeSet.Format != "NezamMonitor.AndroidChanges")
                    {
                        result.Errors.Add("Invalid change set format");
                        return result;
                    }

                    using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}");
                    conn.Open();

                    var appliedCount = 0;
                    foreach (var change in changeSet.Changes)
                    {
                        if (change.Entity == "FollowUp" && change.Field == "description")
                        {
                            using var cmd = conn.CreateCommand();
                            cmd.CommandText = @"INSERT OR REPLACE INTO FollowUpEdits 
                                (CaseNumber, Description, CaseNumberEdit, OwnerEdit, AddressEdit, OwnerMobileEdit, CreatedAt, UpdatedAt) 
                                VALUES (@cn, @desc, '', '', '', '', 
                                    COALESCE((SELECT CreatedAt FROM FollowUpEdits WHERE CaseNumber=@cn), @now), @now)";
                            cmd.Parameters.AddWithValue("@cn", change.RecordId);
                            cmd.Parameters.AddWithValue("@desc", change.NewValue ?? "");
                            cmd.Parameters.AddWithValue("@now", DateTime.Now.ToString("yyyy/MM/dd HH:mm"));
                            cmd.ExecuteNonQuery();
                            appliedCount++;
                        }
                    }

                    result.Success = true;
                    result.AppliedChanges = appliedCount;
                    result.Message = $"Applied {appliedCount} changes from Android (v{changeSet.Version})";
                    return result;
                }
                else if (File.Exists(manifestPath))
                {
                    // Format 2: Full dataset (existing logic)
                    var manifest = JsonSerializer.Deserialize<ImportManifest>(File.ReadAllText(manifestPath));
                    if (manifest == null || manifest.Format != "NezamMonitor.AndroidData")
                    {
                        result.Errors.Add("Invalid package format");
                        return result;
                    }

                    var dataPath = Path.Combine(tempDir, "data.json");
                    if (!File.Exists(dataPath))
                    {
                        result.Errors.Add("Missing data.json");
                        return result;
                    }

                    var dataJson = File.ReadAllText(dataPath);
                    var data = JsonSerializer.Deserialize<ImportData>(dataJson);
                    if (data == null || data.Cases == null)
                    {
                        result.Errors.Add("Invalid data format");
                        return result;
                    }

                    using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}");
                    conn.Open();

                    var appliedCount = 0;
                    foreach (var importCase in data.Cases)
                    {
                        if (importCase.FollowUpEdit != null)
                        {
                            using var cmd = conn.CreateCommand();
                            cmd.CommandText = @"INSERT OR REPLACE INTO FollowUpEdits 
                                (CaseNumber, Description, CaseNumberEdit, OwnerEdit, AddressEdit, OwnerMobileEdit, CreatedAt, UpdatedAt) 
                                VALUES (@cn, @desc, @cnEdit, @ownerEdit, @addrEdit, @mobileEdit, 
                                    COALESCE((SELECT CreatedAt FROM FollowUpEdits WHERE CaseNumber=@cn), @now), @now)";
                            cmd.Parameters.AddWithValue("@cn", importCase.CaseNumber);
                            cmd.Parameters.AddWithValue("@desc", importCase.FollowUpEdit.Description ?? "");
                            cmd.Parameters.AddWithValue("@cnEdit", importCase.FollowUpEdit.CaseNumberEdit ?? "");
                            cmd.Parameters.AddWithValue("@ownerEdit", importCase.FollowUpEdit.OwnerEdit ?? "");
                            cmd.Parameters.AddWithValue("@addrEdit", importCase.FollowUpEdit.AddressEdit ?? "");
                            cmd.Parameters.AddWithValue("@mobileEdit", importCase.FollowUpEdit.OwnerMobileEdit ?? "");
                            cmd.Parameters.AddWithValue("@now", DateTime.Now.ToString("yyyy/MM/dd HH:mm"));
                            cmd.ExecuteNonQuery();
                            appliedCount++;
                        }
                    }

                    result.Success = true;
                    result.AppliedChanges = appliedCount;
                    result.Message = $"Applied {appliedCount} changes from Android (full dataset)";
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

    private static byte[] EncryptString(string plainText, string password)
    {
        using var aes = Aes.Create();
        var key = new Rfc2898DeriveBytes(password, Encoding.UTF8.GetBytes("NezamMonitor2024"), 100000, HashAlgorithmName.SHA256);
        aes.Key = key.GetBytes(32);
        aes.IV = key.GetBytes(16);

        using var encryptor = aes.CreateEncryptor();
        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var writer = new StreamWriter(cs))
        {
            writer.Write(plainText);
        }
        return ms.ToArray();
    }

    private static string DecryptString(byte[] cipherText, string password)
    {
        using var aes = Aes.Create();
        var key = new Rfc2898DeriveBytes(password, Encoding.UTF8.GetBytes("NezamMonitor2024"), 100000, HashAlgorithmName.SHA256);
        aes.Key = key.GetBytes(32);
        aes.IV = key.GetBytes(16);

        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream(cipherText);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var reader = new StreamReader(cs);
        return reader.ReadToEnd();
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
}

public class ImportData
{
    public List<ImportCase> Cases { get; set; } = new();
}

public class ImportCase
{
    public string CaseNumber { get; set; } = "";
    public ImportFollowUpEdit? FollowUpEdit { get; set; }
}

public class ImportFollowUpEdit
{
    public string Description { get; set; } = "";
    public string CaseNumberEdit { get; set; } = "";
    public string OwnerEdit { get; set; } = "";
    public string AddressEdit { get; set; } = "";
    public string OwnerMobileEdit { get; set; } = "";
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
