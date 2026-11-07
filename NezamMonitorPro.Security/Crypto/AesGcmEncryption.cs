using System;
using System.Security.Cryptography;
using System.Text;

namespace NezamMonitorPro.Security.Crypto
{
    /// <summary>
    /// AES-256-GCM encryption/decryption helpers.
    /// </summary>
    public static class AesGcmEncryption
    {
        /// <summary>
        /// Encrypts plaintext using AES-256-GCM.
        /// Returns ciphertext || tag (16 bytes tag appended).
        /// </summary>
        public static byte[] Encrypt(byte[] plaintext, byte[] key, byte[] nonce, byte[] associatedData = null)
        {
            if (key == null || key.Length != 32)
                throw new ArgumentException("Key must be 32 bytes (256 bits)", nameof(key));
            if (nonce == null || nonce.Length != 12)
                throw new ArgumentException("Nonce must be 12 bytes (96 bits)", nameof(nonce));

            var cipher = new AesGcm(key);
            var tag = new byte[16];
            var ciphertext = new byte[plaintext.Length];
            cipher.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
            
            // Return ciphertext || tag
            var result = new byte[ciphertext.Length + tag.Length];
            Buffer.BlockCopy(ciphertext, 0, result, 0, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, result, ciphertext.Length, tag.Length);
            return result;
        }

        /// <summary>
        /// Decrypts ciphertext that was encrypted with Encrypt (ciphertext || tag).
        /// </summary>
        public static byte[] Decrypt(byte[] ciphertextWithTag, byte[] key, byte[] nonce, byte[] associatedData = null)
        {
            if (key == null || key.Length != 32)
                throw new ArgumentException("Key must be 32 bytes (256 bits)", nameof(key));
            if (nonce == null || nonce.Length != 12)
                throw new ArgumentException("Nonce must be 12 bytes (96 bits)", nameof(nonce));
            if (ciphertextWithTag == null || ciphertextWithTag.Length < 16)
                throw new ArgumentException("Ciphertext too short", nameof(ciphertextWithTag));

            var tag = ciphertextWithTag[^16..];
            var ciphertext = ciphertextWithTag[..^16];
            var plaintext = new byte[ciphertext.Length];
            
            var cipher = new AesGcm(key);
            cipher.Decrypt(nonce, ciphertext, tag, plaintext, null);
            return plaintext;
        }

        /// <summary>
        /// Generates a cryptographically secure random nonce (12 bytes).
        /// </summary>
        public static byte[] GenerateNonce()
        {
            var nonce = new byte[12];
            RandomNumberGenerator.Fill(nonce);
            return nonce;
        }

        /// <summary>
        /// Generates a cryptographically secure random key (32 bytes = 256 bits).
        /// </summary>
        public static byte[] GenerateKey()
        {
            var key = new byte[32];
            RandomNumberGenerator.Fill(key);
            return key;
        }
    }
}