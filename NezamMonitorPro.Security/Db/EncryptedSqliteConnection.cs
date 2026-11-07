using System;
using System.Data.Common;
using System.IO;
using System.Text;
using Microsoft.Data.Sqlite;

namespace NezamMonitorPro.Security.Db
{
    public sealed class EncryptedSqliteConnection : IDisposable
    {
        private readonly SqliteConnection _connection;
        private bool _disposed;

        static EncryptedSqliteConnection()
        {
            try
            {
                SQLitePCL.Batteries_V2.Init();
            }
            catch { }
        }

        public EncryptedSqliteConnection(string databasePath, string password, SqliteOpenMode openMode = SqliteOpenMode.ReadWriteCreate)
        {
            if (string.IsNullOrEmpty(databasePath))
                throw new ArgumentException("Database path cannot be null or empty", nameof(databasePath));
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("Password cannot be null or empty", nameof(password));

            var logPath = Path.Combine(
                Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName)
                ?? AppDomain.CurrentDomain.BaseDirectory,
                "startup.log");

            try
            {
                File.AppendAllText(logPath, $"[{DateTime.Now}] EncryptedSqliteConnection -> Opening DB: {databasePath}, PasswordLen: {password.Length}\n");
            }
            catch { }

            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = openMode,
                Cache = SqliteCacheMode.Shared,
                ForeignKeys = true,
                Password = password
            };

            _connection = new SqliteConnection(builder.ConnectionString);
            
            try
            {
                _connection.Open();
            }
            catch (Exception ex)
            {
                try { File.AppendAllText(logPath, $"[{DateTime.Now}] Connection Open FAIL: {ex.Message}\n"); } catch { }
                throw new UnauthorizedAccessException("Invalid password.", ex);
            }

            try
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "PRAGMA cipher_version;";
                var version = cmd.ExecuteScalar();
                File.AppendAllText(logPath, $"[{DateTime.Now}] Connection Open OK, SQLCipher version: {version}\n");
            }
            catch (Exception ex)
            {
                try { File.AppendAllText(logPath, $"[{DateTime.Now}] Cipher version check FAIL: {ex.Message}\n"); } catch { }
            }
        }

        public SqliteConnection Connection => _connection;
        public DbCommand CreateCommand() => _connection.CreateCommand();
        public DbTransaction BeginTransaction() => _connection.BeginTransaction();

        public void Rekey(string newPassword)
        {
            if (string.IsNullOrEmpty(newPassword))
                throw new ArgumentException("New password cannot be null or empty", nameof(newPassword));

            using var cmd = _connection.CreateCommand();
            // SQLite/SQLCipher PRAGMA statements do not accept parameter binding (@newPassword).
            // The password must be formatted directly as a string literal.
            var escapedPassword = newPassword.Replace("'", "''");
            cmd.CommandText = $"PRAGMA rekey = '{escapedPassword}';";
            cmd.ExecuteNonQuery();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _connection?.Dispose();
                _disposed = true;
            }
        }
    }
}
