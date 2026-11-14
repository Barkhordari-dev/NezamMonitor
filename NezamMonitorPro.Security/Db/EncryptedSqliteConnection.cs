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
                throw new UnauthorizedAccessException("Invalid password.", ex);
            }

            // باندل SQLCipher باید نسخه cipher را برگرداند؛ اگر خالی بود یعنی
            // باندل plain است و برنامه نباید با آن بالا بیاید
            string? cipherVersion;
            try
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "PRAGMA cipher_version;";
                cipherVersion = cmd.ExecuteScalar()?.ToString();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("باندل SQLCipher در دسترس نیست؛ برنامه با دیتابیس رمزنگاری‌نشده بالا نمی‌آید.", ex);
            }

            if (string.IsNullOrWhiteSpace(cipherVersion))
                throw new InvalidOperationException("باندل SQLCipher در دسترس نیست؛ برنامه با دیتابیس رمزنگاری‌نشده بالا نمی‌آید.");
        }

        public SqliteConnection Connection => _connection;
        public DbCommand CreateCommand() => _connection.CreateCommand();
        public DbTransaction BeginTransaction() => _connection.BeginTransaction();

        public void Rekey(string newPassword)
        {
            if (string.IsNullOrEmpty(newPassword))
                throw new ArgumentException("New password cannot be null or empty", nameof(newPassword));

            // تذکر: sqlite3_rekey خام در باندل هست، ولی Microsoft.Data.Sqlite هندل
            // خام اتصال را بیرون نمی‌دهد؛ پس رمز را با همان PRAGMA عوض می‌کنیم و
            // ورودی را سخت‌گیرانه اعتبارسنجی می‌کنیم تا رشته‌سازی امن بماند
            foreach (var ch in newPassword)
            {
                if (ch == '\0' || char.IsControl(ch))
                    throw new ArgumentException("رمز عبور نمی‌تواند کاراکتر کنترلی داشته باشد.", nameof(newPassword));
            }

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
