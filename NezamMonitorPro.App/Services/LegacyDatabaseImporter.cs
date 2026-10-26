using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using NezamMonitorPro.Core.Data;

namespace NezamMonitorPro.App.Services;

public static class LegacyDatabaseImporter
{
    public sealed record ImportResult(bool Success, int Imported, string Message);

    public static ImportResult Import(NezamDatabase target, string sourcePath)
    {
        if (!File.Exists(sourcePath))
            return new ImportResult(false, 0, "فایل پیدا نشد.");

        var dst = target.Connection;
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
                using var reader = ExecuteReader(source, $"SELECT * FROM \"{srcTables["snapshots"]}\" ORDER BY Id");
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
                }
            }

            // ۳. کپی پرونده‌ها (بسیار سریع با استفاده از Prepared Command)
            if (srcTables.ContainsKey("cases") && snapIdMap.Count > 0)
            {
                var caseIdMap = new Dictionary<long, long>();
                
                // آماده‌سازی دستور Insert پرونده یکبار برای همیشه
                var caseCols = GetTableColumns(dst, "Cases").Where(c => c != "Id").ToList();
                var insertCaseCmd = CreateInsertCommand(dst, "Cases", caseCols, transaction);

                using (var reader = ExecuteReader(source, $"SELECT * FROM \"{srcTables["cases"]}\""))
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

                    using var reader = ExecuteReader(source, $"SELECT * FROM \"{srcTables[srcKey]}\"");
                    var srcCols = GetReaderColumns(reader);
                    
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
                    }
                }
            }

            // ۵. کپی تنظیمات و موارد عمومی (Insert or Replace)
            var globals = new[] { "Settings", "FollowUpEdits", "EngineerRegistry", "FeeReceipts", "GeneratedReports" };
            foreach (var table in globals)
            {
                var srcKey = table.ToLower();
                if (!srcTables.ContainsKey(srcKey)) continue;

                var cols = GetTableColumns(dst, table).Where(c => c != "Id").ToList();
                var insertCmd = CreateInsertOrReplaceCommand(dst, table, cols, transaction);

                using var reader = ExecuteReader(source, $"SELECT * FROM \"{srcTables[srcKey]}\"");
                var srcCols = GetReaderColumns(reader);
                while (reader.Read())
                {
                    foreach (var col in cols)
                    {
                        insertCmd.Parameters["@" + col].Value = srcCols.Contains(col) ? reader[col] : DBNull.Value;
                    }
                    insertCmd.ExecuteNonQuery();
                }
            }

            transaction.Commit();
            
            // بازگرداندن تنظیمات به حالت امن
            Exec(dst, "PRAGMA synchronous = NORMAL;");

            return new ImportResult(true, caseCount, 
                $"ایمپورت فوق‌سریع انجام شد: {snapshotCount} اسنپ‌شات و {caseCount} پرونده منتقل شدند.");
        }
        catch (Exception ex)
        {
            return new ImportResult(false, 0, "خطا در ایمپورت سریع: " + ex.Message);
        }
    }

    private static Dictionary<string, string> GetTableMap(SqliteConnection conn)
    {
        var map = new Dictionary<string, string>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
        using var r = cmd.ExecuteReader();
        while (r.Read()) { var n = r.GetString(0); map[n.ToLower()] = n; }
        return map;
    }

    private static List<string> GetTableColumns(SqliteConnection conn, string table)
    {
        var list = new List<string>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{table}\")";
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
        var sql = $"INSERT INTO \"{table}\" ({string.Join(",", cols.Select(c => $"\"{c}\""))}) VALUES ({string.Join(",", cols.Select(c => "@" + c))}); SELECT last_insert_rowid();";
        var cmd = new SqliteCommand(sql, conn, trans);
        foreach (var col in cols) cmd.Parameters.Add("@" + col, SqliteType.Text); // پیش‌فرض متن
        return cmd;
    }

    private static SqliteCommand CreateInsertOrReplaceCommand(SqliteConnection conn, string table, List<string> cols, SqliteTransaction trans)
    {
        var sql = $"INSERT OR REPLACE INTO \"{table}\" ({string.Join(",", cols.Select(c => $"\"{c}\""))}) VALUES ({string.Join(",", cols.Select(c => "@" + c))});";
        var cmd = new SqliteCommand(sql, conn, trans);
        foreach (var col in cols) cmd.Parameters.Add("@" + col, SqliteType.Text);
        return cmd;
    }

    private static long InsertSingleRow(SqliteConnection conn, string table, Dictionary<string, object?> values, SqliteTransaction trans)
    {
        var cols = values.Keys.ToList();
        var sql = $"INSERT INTO \"{table}\" ({string.Join(",", cols.Select(c => $"\"{c}\""))}) VALUES ({string.Join(",", cols.Select(c => "@" + c))}); SELECT last_insert_rowid();";
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
