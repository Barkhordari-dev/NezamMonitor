using System;
using System.IO;
using System.Text.Json;
using NezamMonitorPro.Security.Crypto;

namespace NezamMonitorPro.Security.Services
{
    /// <summary>
    /// Data structure for stored credentials.
    /// </summary>
    public sealed class CredentialData
    {
        public byte[] Hash { get; set; }
        public byte[] Salt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int Argon2Iterations { get; set; } = Argon2PasswordHasher.Iterations;
        public int Argon2MemoryKib { get; set; } = Argon2PasswordHasher.MemoryKib;
        public int Argon2Parallelism { get; set; } = Argon2PasswordHasher.Parallelism;
    }

    /// <summary>
    /// Manages user credentials (password hash storage, verification, changes).
    /// Stores Argon2id hash + salt in a JSON file.
    /// </summary>
    public sealed class CredentialStore
    {
        private readonly string _filePath;
        private readonly object _lock = new();

        /// <summary>
        /// Creates a new CredentialStore.
        /// </summary>
        /// <param name="filePath">Path to the credentials JSON file.</param>
        public CredentialStore(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
            _filePath = filePath;
        }

        /// <summary>
        /// Checks if a password has been set.
        /// </summary>
        public bool HasPassword()
        {
            lock (_lock)
            {
                return File.Exists(_filePath);
            }
        }

        /// <summary>
        /// Loads the credential data from disk.
        /// </summary>
        public CredentialData Load()
        {
            lock (_lock)
            {
                if (!File.Exists(_filePath))
                    throw new InvalidOperationException("No password has been set yet.");

                var json = File.ReadAllText(_filePath);
                var data = JsonSerializer.Deserialize<CredentialData>(json);
                if (data == null)
                    throw new InvalidOperationException("Failed to deserialize credential data.");
                return data;
            }
        }

        /// <summary>
        /// Saves the credential data to disk.
        /// </summary>
        private void Save(CredentialData data)
        {
            lock (_lock)
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(data, options);
                File.WriteAllText(_filePath, json);
            }
        }

        /// <summary>
        /// Sets a new password (creates or overwrites).
        /// </summary>
        public void SetPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));

            var (hash, salt) = Argon2PasswordHasher.Hash(password);
            var data = new CredentialData
            {
                Hash = hash,
                Salt = salt,
                CreatedAt = DateTime.UtcNow
            };
            Save(data);
        }

        /// <summary>
        /// Verifies a password against the stored hash.
        /// </summary>
        public bool VerifyPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                return false;

            lock (_lock)
            {
                if (!File.Exists(_filePath))
                    return false;

                try
                {
                    var data = Load();
                    return Argon2PasswordHasher.Verify(password, data.Hash, data.Salt);
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Changes the password (requires current password).
        /// </summary>
        public void ChangePassword(string currentPassword, string newPassword)
        {
            if (string.IsNullOrEmpty(currentPassword))
                throw new ArgumentException("Current password cannot be empty", nameof(currentPassword));
            if (string.IsNullOrEmpty(newPassword))
                throw new ArgumentException("New password cannot be empty", nameof(newPassword));
            if (currentPassword == newPassword)
                throw new ArgumentException("New password must be different from current password");

            lock (_lock)
            {
                if (!VerifyPassword(currentPassword))
                    throw new UnauthorizedAccessException("Current password is incorrect");

                SetPassword(newPassword);
            }
        }

        /// <summary>
        /// Resets the password (admin/reset flow - no current password required).
        /// </summary>
        public void ResetPassword(string newPassword)
        {
            if (string.IsNullOrEmpty(newPassword))
                throw new ArgumentException("New password cannot be empty", nameof(newPassword));
            SetPassword(newPassword);
        }

        /// <summary>
        /// Removes the stored password.
        /// </summary>
        public void ClearPassword()
        {
            lock (_lock)
            {
                if (File.Exists(_filePath))
                    File.Delete(_filePath);
            }
        }
    }
}