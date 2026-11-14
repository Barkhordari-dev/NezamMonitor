using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using NezamMonitorPro.Core.Data;

namespace NezamMonitorPro.App.Services;

public static class LegacyDatabaseImporter
{
    public sealed record ImportResult(bool Success, int Imported, string Message);

    // سقف حجم فایل مبدا و سقف ردیف برای هر جدول
    private const long MaxFileBytes = 500L * 1024 * 1024;
    private const int MaxRowsPerTable = 200_000;

    public static ImportResult Import(NezamDatabase target, string sourcePath)
    {
        // ARCH-003: کل ایمپورت مستقیم روی اتصال مشترک کار می‌کند؛
        // زیر قفل اتصال انجام می‌شود تا با نخ‌های دیگر تداخل نکند.
        lock (target.SyncRoot)
            return ImportCore(target, sourcePath);
    }

    private static ImportResult ImportCore(NezamDatabase target, string sourcePath)
    {
        if (!File.Exists(sourcePath))
            return new ImportResult(false, 0, "فایل پیدا نشد.");

        // فایل مبدا نباید غول‌پیکر باشد؛ بیشتر از این یعنی اشتباه یا خراب است
        if (new FileInfo(sourcePath).Length > MaxFileBytes)
            return new ImportResult(false, 0, "فایل مبدا خیلی بزرگ است؛ حداکثر ۵۰۰ مگابایت پذیرفته می‌شود.");

        var dst = target.Connection;

        // مقدار فعلی PRAGMAها را نگه دار تا در finally به همان‌ها برگردند
        var prevSync = "NORMAL";
        var prevJournal = "delete";
        try
        {
            prevSync = QueryPragma(dst, "synchronous");
            prevJournal = QueryPragma(dst, "journal_mode");
        }
        catch { /* اگر خواندنش نشد، با مقدار پیش‌فرض ادامه بده */ }

        try
        {
            var builder = new SqliteConnectionStringBuilder { DataSource = sourcePath, Mode = SqliteOpenMode.ReadOnly };
            using var source = new SqliteConnection(builder.ToString());
            source.Open();

            // ۱. بهینه‌سازی سرعت دیتابیس مقصد (فقط برای این نشست)
            Exec(dst, "PRAGMA synchronous = OFF;");
            Exec(dst, "PRAGMA journal_mode = MEMORY;");

            using var transaction = dst.BeginTransaction();
            
            var srcTables = GetTableMap(source);
            var dstTables = GetTableMap(dst);

            int snapshotCount = 0;
            int caseCount = 0;
            int childCount = 0;
            var snapIdMap = new Dictionary<long, long>();

            // ۲. کپی اسنپ‌شات‌ها (سریع)
            if (srcTables.ContainsKey("snapshots"))
            {
                using var reader = ExecuteReader(source, $"SELECT * FROM {QuoteIdent(srcTables["snapshots"])} ORDER BY Id");
                var columns = GetReaderColumns(reader);
                
                while (reader.Read())
                {
                    var oldId = reader.GetInt64(reader.GetOrdinal("Id"));
                    var createdAt = GetValue(reader, "CreatedAt")?.ToString() ?? DateTime.Now.ToString("O");
                    
                    var newId = InsertSingleRow(dst, "Snapshots", new Dictionary<string, object?> {
                        ["CreatedAt"] = createdAt,
                        ["CaseCount"] = 0,
                        ["Status"] = "done"
                    }, transaction);
                    
                    snapIdMap[oldId] = newId;
                    snapshotCount++;
                    CheckRowLimit(snapshotCount, "Snapshots");
                }
            }

            // ۳. کپی پرونده‌ها (بسیار سریع با استفاده از Prepared Command)
            if (srcTables.ContainsKey("cases") && snapIdMap.Count > 0)
            {
                var caseIdMap = new Dictionary<long, long>();
                
                // آماده‌سازی دستور Insert پرونده یکبار برای همیشه
                var caseCols = GetTableColumns(dst, "Cases").Where(c => c != "Id").ToList();
                var insertCaseCmd = CreateInsertCommand(dst, "Cases", caseCols, transaction);

                using (var reader = ExecuteReader(source, $"SELECT * FROM {QuoteIdent(srcTables["cases"])}"))
                {
                    var srcCols = GetReaderColumns(reader);
                    while (reader.Read())
                    {
                        var oldId = reader.GetInt64(reader.GetOrdinal("Id"));
                        var oldSnapId = reader.GetInt64(reader.GetOrdinal("SnapshotId"));
                        
                        if (!snapIdMap.TryGetValue(oldSnapId, out var newSnapId)) continue;

                        // پر کردن پارامترهای دستور آماده شده
                        foreach (var col in caseCols)
                        {
                            var paramName = "@" + col;
                            if (col == "SnapshotId") insertCaseCmd.Parameters[paramName].Value = newSnapId;
                            else insertCaseCmd.Parameters[paramName].Value = srcCols.Contains(col) ? reader[col] : DBNull.Value;
                        }

                        var newId = Convert.ToInt64(insertCaseCmd.ExecuteScalar());
                        caseIdMap[oldId] = newId;
                        caseCount++;
                        CheckRowLimit(caseCount, "Cases");
                    }
                }

            // ۳.۵ به‌روزرسانی CaseCount برای تمام اسنپ‌شات‌ها
            foreach (var snapId in snapIdMap.Values)
            {
                Exec(dst, $"UPDATE Snapshots SET CaseCount = (SELECT COUNT(*) FROM Cases WHERE SnapshotId = {snapId}) WHERE Id = {snapId};");
            }

            // ۴. کپی جداول وابسته (مشخصات، مهندسین، فیش، گزارش)
                var childTables = new[] { "Specifications", "Engineers", "Fees", "Reports" };
                foreach (var table in childTables)
                {
                    var srcKey = table.ToLower();
                    if (!srcTables.ContainsKey(srcKey)) continue;

                    var cols = GetTableColumns(dst, table).Where(c => c != "Id").ToList();
                    var insertCmd = CreateInsertCommand(dst, table, cols, transaction);

                    using var reader = ExecuteReader(source, $"SELECT * FROM {QuoteIdent(srcTables[srcKey])}");
                    var srcCols = GetReaderColumns(reader);
                    var rows = 0;
                    while (reader.Read())
                    {
                        var oldCaseId = reader.GetInt64(reader.GetOrdinal("CaseId"));
                        if (!caseIdMap.TryGetValue(oldCaseId, out var newCaseId)) continue;

                        foreach (var col in cols)
                        {
                            var paramName = "@" + col;
                            if (col == "CaseId") insertCmd.Parameters[paramName].Value = newCaseId;
                            else insertCmd.Parameters[paramName].Value = srcCols.Contains(col) ? reader[col] : DBNull.Value;
                        }
                        insertCmd.ExecuteNonQuery();
                        childCount++;
                        CheckRowLimit(++rows, table);
                    }
                }
            }

            // ۵. کپی موارد عمومی (Insert or Replace) — جدول Settings عمدا کپی
            // نمی‌شود؛ تنظیمات فایل قدیمی (از جمله رمز و توکن) نباید روی تنظیمات
            // فعلی برنامه سوار شود
            var globals = new[] { "FollowUpEdits", "EngineerRegistry", "FeeReceipts", "GeneratedReports" };
            foreach (var table in globals)
            {
                var srcKey = table.ToLower();
                if (!srcTables.ContainsKey(srcKey)) continue;

                var cols = GetTableColumns(dst, table).Where(c => c != "Id").ToList();
                var insertCmd = CreateInsertOrReplaceCommand(dst, table, cols, transaction);

                using var reader = ExecuteReader(source, $"SELECT * FROM {QuoteIdent(srcTables[srcKey])}");
                var srcCols = GetReaderColumns(reader);
                var rows = 0;
                while (reader.Read())
                {
                    foreach (var col in cols)
                    {
                        insertCmd.Parameters["@" + col].Value = srcCols.Contains(col) ? reader[col] : DBNull.Value;
                    }
                    insertCmd.ExecuteNonQuery();
                    CheckRowLimit(++rows, table);
                }
            }

            transaction.Commit();

            return new ImportResult(true, caseCount, 
                $"ایمپورت فوق‌سریع انجام شد: {snapshotCount} اسنپ‌شات و {caseCount} پرونده منتقل شدند.");
        }
        catch (Exception ex)
        {
            return new ImportResult(false, 0, "خطا در ایمپورت سریع: " + ex.Message);
        }
        finally
        {
            // در هر صورت (موفق یا خطا) تنظیمات را به حالت قبلی برگردان
            // تا اتصال مشترک با synchronous=OFF یا journal=MEMORY رها نشود
            try { Exec(dst, $"PRAGMA synchronous = {prevSync};"); } catch { }
            try { Exec(dst, $"PRAGMA journal_mode = {prevJournal};"); } catch { }
        }
    }

    // نام جدول/ستون فقط باید حروف، عدد و underscore باشد؛ هر چیز دیگری
    // (از جمله " و ; و فاصله) یعنی نام دست‌کاری‌شده و نباید وارد SQL شود
    private static bool IsSafeIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        foreach (var ch in name)
        {
            if (!(ch == '_' || (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9')))
                return false;
        }
        return true;
    }

    // نام را داخل "..." می‌گذارد و " داخلی را با "" خنثی می‌کند؛
    // نام ناامن خطا می‌دهد تا به SQL نرسد
    private static string QuoteIdent(string name)
    {
        if (!IsSafeIdentifier(name))
            throw new ArgumentException($"نام جدول/ستون نامعتبر است: {name}", nameof(name));
        return "\"" + name.Replace("\"", "\"\"") + "\"";
    }

    private static void CheckRowLimit(int count, string table)
    {
        if (count > MaxRowsPerTable)
            throw new InvalidOperationException($"جدول «{table}» بیشتر از ۲۰۰٬۰۰۰ ردیف دارد؛ ایمپورت متوقف شد.");
    }

    private static string QueryPragma(SqliteConnection conn, string name)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA {name};";
        return cmd.ExecuteScalar()?.ToString() ?? "";
    }

    private static Dictionary<string, string> GetTableMap(SqliteConnection conn)
    {
        var map = new Dictionary<string, string>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var n = r.GetString(0);
            // نام جدول از فایل مبدا می‌آید؛ فقط نام‌های ساده قبول‌اند
            if (!IsSafeIdentifier(n)) continue;
            map[n.ToLower()] = n;
        }
        return map;
    }

    private static List<string> GetTableColumns(SqliteConnection conn, string table)
    {
        var list = new List<string>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({QuoteIdent(table)})";
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(r.GetString(1));
        return list;
    }

    private static HashSet<string> GetReaderColumns(SqliteDataReader reader)
    {
        var hs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < reader.FieldCount; i++) hs.Add(reader.GetName(i));
        return hs;
    }

    private static SqliteCommand CreateInsertCommand(SqliteConnection conn, string table, List<string> cols, SqliteTransaction trans)
    {
        var sql = $"INSERT INTO {QuoteIdent(table)} ({string.Join(",", cols.Select(c => QuoteIdent(c)))}) VALUES ({string.Join(",", cols.Select(c => "@" + c))}); SELECT last_insert_rowid();";
        var cmd = new SqliteCommand(sql, conn, trans);
        foreach (var col in cols) cmd.Parameters.Add("@" + col, SqliteType.Text); // پیش‌فرض متن
        return cmd;
    }

    private static SqliteCommand CreateInsertOrReplaceCommand(SqliteConnection conn, string table, List<string> cols, SqliteTransaction trans)
    {
        var sql = $"INSERT OR REPLACE INTO {QuoteIdent(table)} ({string.Join(",", cols.Select(c => QuoteIdent(c)))}) VALUES ({string.Join(",", cols.Select(c => "@" + c))});";
        var cmd = new SqliteCommand(sql, conn, trans);
        foreach (var col in cols) cmd.Parameters.Add("@" + col, SqliteType.Text);
        return cmd;
    }

    private static long InsertSingleRow(SqliteConnection conn, string table, Dictionary<string, object?> values, SqliteTransaction trans)
    {
        var cols = values.Keys.ToList();
        var sql = $"INSERT INTO {QuoteIdent(table)} ({string.Join(",", cols.Select(c => QuoteIdent(c)))}) VALUES ({string.Join(",", cols.Select(c => "@" + c))}); SELECT last_insert_rowid();";
        using var cmd = new SqliteCommand(sql, conn, trans);
        foreach (var kv in values) cmd.Parameters.AddWithValue("@" + kv.Key, kv.Value ?? DBNull.Value);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private static SqliteDataReader ExecuteReader(SqliteConnection conn, string sql)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteReader();
    }

    private static void Exec(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static object? GetValue(SqliteDataReader r, string col) 
    {
        try { return r[col]; } catch { return null; }
    }
}
