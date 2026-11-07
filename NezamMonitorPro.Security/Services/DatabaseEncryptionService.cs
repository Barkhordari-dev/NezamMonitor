using System;
using System.IO;
using System.Text.Json;
using NezamMonitorPro.Security.Crypto;
using NezamMonitorPro.Security.Db;

namespace NezamMonitorPro.Security.Services
{
    public sealed class DatabaseEncryptionService
    {
        private readonly CredentialStore _credentialStore;
        private readonly string _databasePath;

        public DatabaseEncryptionService(CredentialStore credentialStore, string databasePath)
        {
            _credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
            _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
        }

        public EncryptedSqliteConnection Open(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));

            var logPath = Path.Combine(Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName) ?? AppDomain.CurrentDomain.BaseDirectory, "startup.log");
            bool verified = _credentialStore.VerifyPassword(password);
            try { File.AppendAllText(logPath, $"[{DateTime.Now}] DatabaseEncryptionService.Open VerifyPassword result: {verified}, passwordLen: {password?.Length}\n"); } catch { }

            if (!verified)
                throw new UnauthorizedAccessException("Invalid password.");

            return new EncryptedSqliteConnection(_databasePath, password);
        }

        public void CreateDatabase(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));

            var (hash, salt) = Argon2PasswordHasher.Hash(password);
            var credData = new Services.CredentialData
            {
                Hash = hash,
                Salt = salt,
                CreatedAt = DateTime.UtcNow
            };

            SaveCredentials(credData);

            using var conn = new EncryptedSqliteConnection(_databasePath, password);
        }

        public void ChangePassword(string currentPassword, string newPassword)
        {
            if (string.IsNullOrEmpty(currentPassword))
                throw new ArgumentException("Current password cannot be empty", nameof(currentPassword));
            if (string.IsNullOrEmpty(newPassword))
                throw new ArgumentException("New password cannot be empty", nameof(newPassword));
            if (currentPassword == newPassword)
                throw new ArgumentException("New password must be different from current password");

            if (!_credentialStore.VerifyPassword(currentPassword))
                throw new UnauthorizedAccessException("Current password is incorrect");

            using var conn = new EncryptedSqliteConnection(_databasePath, currentPassword);
            conn.Rekey(newPassword);

            _credentialStore.ChangePassword(currentPassword, newPassword);
        }

        private Services.CredentialData LoadCredentials() => _credentialStore.Load();

        private void SaveCredentials(Services.CredentialData data)
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            var dir = Path.GetDirectoryName(_credentialStore.GetType().GetField("_filePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(_credentialStore) as string);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(GetCredentialFilePath(), json);
        }

        private string GetCredentialFilePath()
        {
            var field = _credentialStore.GetType().GetField("_filePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(_credentialStore) as string ?? "";
        }
    }
}
