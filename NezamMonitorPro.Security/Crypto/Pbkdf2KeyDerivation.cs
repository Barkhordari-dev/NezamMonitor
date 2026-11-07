using System;
using System.Security.Cryptography;

namespace NezamMonitorPro.Security.Crypto
{
    /// <summary>
    /// PBKDF2 key derivation with secure defaults.
    /// </summary>
    public static class Pbkdf2KeyDerivation
    {
        /// <summary>
        /// Derives a cryptographic key from a password using PBKDF2-HMAC-SHA256.
        /// </summary>
        /// <param name="password">The password to derive the key from.</param>
        /// <param name="salt">The salt (minimum 16 bytes recommended).</param>
        /// <param name="iterations">Number of iterations (default 600,000 per OWASP 2023).</param>
        /// <param name="keyLength">Desired key length in bytes (default 32 for AES-256).</param>
        /// <returns>Derived key.</param>
        public static byte[] Derive(string password, byte[] salt, int iterations = 600_000, int keyLength = 32)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));
            if (salt == null || salt.Length < 16)
                throw new ArgumentException("Salt must be at least 16 bytes", nameof(salt));
            if (iterations < 100_000)
                throw new ArgumentException("Iterations should be at least 100,000", nameof(iterations));
            if (keyLength < 16)
                throw new ArgumentException("Key length should be at least 16 bytes", nameof(keyLength));

            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
            return pbkdf2.GetBytes(keyLength);
        }

        /// <summary>
        /// Derives a key from a password with a randomly generated salt.
        /// Returns (key, salt).
        /// </summary>
        public static (byte[] key, byte[] salt) DeriveWithNewSalt(string password, int iterations = 600_000, int keyLength = 32, int saltSize = 16)
        {
            var salt = RandomNumberGenerator.GetBytes(saltSize);
            var key = Derive(password, salt, iterations, keyLength);
            return (key, salt);
        }

        /// <summary>
        /// Verifies a password against a derived key and salt.
        /// Uses constant-time comparison.
        /// </summary>
        public static bool Verify(string password, byte[] expectedKey, byte[] salt, int iterations = 600_000, int keyLength = 32)
        {
            if (string.IsNullOrEmpty(password))
                return false;
            if (expectedKey == null || expectedKey.Length != keyLength)
                throw new ArgumentException("Invalid expected key length", nameof(expectedKey));
            if (salt == null || salt.Length < 16)
                throw new ArgumentException("Invalid salt length", nameof(salt));

            var derivedKey = Derive(password, salt, iterations, keyLength);
            return CryptographicOperations.FixedTimeEquals(expectedKey, derivedKey);
        }
    }
}