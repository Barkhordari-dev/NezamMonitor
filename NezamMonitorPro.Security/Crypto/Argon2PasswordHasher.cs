using System;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace NezamMonitorPro.Security.Crypto
{
    public static class Argon2PasswordHasher
    {
        public const int SaltSize = 16;
        public const int HashSize = 32;
        public const int Iterations = 3;
        public const int MemoryKib = 65536; // 64 MiB
        public const int Parallelism = 4;

        public static (byte[] hash, byte[] salt) Hash(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));

            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
            {
                Salt = salt,
                DegreeOfParallelism = Parallelism,
                Iterations = Iterations,
                MemorySize = MemoryKib
            };
            var hash = argon2.GetBytes(HashSize);
            return (hash, salt);
        }

        public static bool Verify(string password, byte[] hash, byte[] salt)
        {
            if (string.IsNullOrEmpty(password))
                return false;
            if (hash == null || hash.Length != 32)
                throw new ArgumentException("Invalid hash length", nameof(hash));
            if (salt == null || salt.Length != 16)
                throw new ArgumentException("Invalid salt length", nameof(salt));

            var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
            {
                Salt = salt,
                DegreeOfParallelism = Parallelism,
                Iterations = Iterations,
                MemorySize = MemoryKib
            };
            var computedHash = argon2.GetBytes(HashSize);
            return CryptographicOperations.FixedTimeEquals(hash, computedHash);
        }

        public static (byte[] hash, byte[] salt) Hash(string password, int iterations, int memoryKib, int parallelism)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));

            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
            {
                Salt = salt,
                DegreeOfParallelism = parallelism,
                Iterations = iterations,
                MemorySize = memoryKib
            };
            var hash = argon2.GetBytes(HashSize);
            return (hash, salt);
        }
    }
}
