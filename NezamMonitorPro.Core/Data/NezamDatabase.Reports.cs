using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using NezamMonitorPro.Core.Models;

namespace NezamMonitorPro.Core.Data;

public sealed partial class NezamDatabase
{
    private void LoadReportsBatch(Dictionary<long, Case> byId, long snapshotId)
    {
        lock (_syncRoot)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                SELECT r.* FROM Reports r
                INNER JOIN Cases c ON c.Id = r.CaseId
                WHERE c.SnapshotId = @sid ORDER BY r.rowid
                """;
            cmd.Parameters.AddWithValue("@sid", snapshotId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                if (!byId.TryGetValue(Convert.ToInt64(r["CaseId"]), out var c)) continue;
                c.Reports.Add(new ReportRecord(r.GetString("RowNo"), r.GetString("ReportType"), r.GetString("Stage"),
                    r.GetString("Engineer"), r.GetString("Discipline"), r.GetString("VisitDate"),
                    r.GetString("CeilingCount"), r.GetString("Indicator"), r.GetInt32("HasFile") == 1,
                    SafeGetString(r, "ImageName")));
            }
        }
    }

    /// <summary>
    /// دریافت تعداد گزارش‌ها برای یک پرونده
    /// </summary>
    public int LoadReportCountForCase(string caseNumber)
    {
        lock (_syncRoot)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT COUNT(*) FROM Reports r 
                               INNER JOIN Cases c ON r.CaseId = c.Id 
                               WHERE c.CaseNumber = @cn";
            cmd.Parameters.AddWithValue("@cn", caseNumber);
            return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
        }
    }
}
