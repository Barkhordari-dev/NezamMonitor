using System.Globalization;
using Microsoft.Data.Sqlite;

namespace NezamMonitorPro.Core.Data;

/// <summary>
/// Re-applies hidden-menu overrides onto the database (mirrors Android's
/// applyHiddenOverrides). Works on a raw connection so both the sync path
/// (ExtractionController) and the import path (BackupEngine) can call it
/// without opening a second connection. NEVER throws: a failure on one
/// override never stops the others, and never fails the surrounding
/// sync/import. An override whose row no longer exists is kept so it can
/// apply again if the row comes back.
/// </summary>
public static class HiddenOverrideApplier
{
    /// <summary>Re-applies every permanent override, then deletes the
    /// temporary ones (their "until next sync" life is over — the sync just
    /// saved the server values).</summary>
    public static void ApplyAll(SqliteConnection conn)
    {
        try
        {
            if (!TableExists(conn)) return;
            var overrides = ReadAll(conn);
            foreach (var ov in overrides)
            {
                if (!string.Equals(ov.Mode, "perm", StringComparison.OrdinalIgnoreCase)) continue;
                try { ApplyOne(conn, ov); } catch { }
            }
            try
            {
                using var del = conn.CreateCommand();
                del.CommandText = "DELETE FROM HiddenOverrides WHERE Mode='temp'";
                del.ExecuteNonQuery();
            }
            catch { }
        }
        catch { }
    }

    /// <summary>Applies one override right now. Returns true when a row was
    /// actually updated. Never throws.</summary>
    public static bool ApplyOne(SqliteConnection conn, HiddenOverride ov)
    {
        try
        {
            if (!TableExists(conn)) return false;
            var sid = GetActiveSnapshotId(conn);
            if (sid == 0) return false;
            return ov.RecordType switch
            {
                "case" => ApplyCase(conn, ov, sid),
                "fee" => ApplyFee(conn, ov, sid),
                "engineer" => ApplyEngineer(conn, ov, sid),
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Current time as a Jalali "yyyy/MM/dd HH:mm" stamp (the same
    /// format FollowUpEdits.UpdatedAt uses).</summary>
    public static string NowJalali()
    {
        var pc = new PersianCalendar();
        var now = DateTime.Now;
        return $"{pc.GetYear(now):0000}/{pc.GetMonth(now):00}/{pc.GetDayOfMonth(now):00} {now:HH:mm}";
    }

    private static bool TableExists(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='HiddenOverrides'";
        return Convert.ToInt64(cmd.ExecuteScalar() ?? 0) > 0;
    }

    private static List<HiddenOverride> ReadAll(SqliteConnection conn)
    {
        var list = new List<HiddenOverride>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, RecordType, RecordKey, Field, Value, Mode, UpdatedAt FROM HiddenOverrides";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new HiddenOverride
            {
                Id = r.IsDBNull(0) ? 0 : r.GetInt64(0),
                RecordType = r.IsDBNull(1) ? "" : r.GetString(1),
                RecordKey = r.IsDBNull(2) ? "" : r.GetString(2),
                Field = r.IsDBNull(3) ? "" : r.GetString(3),
                Value = r.IsDBNull(4) ? "" : r.GetString(4),
                Mode = r.IsDBNull(5) ? "perm" : r.GetString(5),
                UpdatedAt = r.IsDBNull(6) ? "" : r.GetString(6)
            });
        }
        return list;
    }

    private static int GetActiveSnapshotId(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT SnapshotId FROM ActiveSnapshot WHERE Id=1";
        var result = cmd.ExecuteScalar();
        if (result != null && Convert.ToInt32(result) is var sid and > 0) return sid;
        using var fallback = conn.CreateCommand();
        fallback.CommandText = "SELECT MAX(Id) FROM Snapshots";
        var max = fallback.ExecuteScalar();
        return max == null || max is DBNull ? 0 : Convert.ToInt32(max);
    }

    private static string CaseIdsSql => "SELECT Id FROM Cases WHERE CaseNumber=@cn AND SnapshotId=@sid";

    private static int Exec(SqliteConnection conn, string sql, params (string Name, object Value)[] args)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args)
            cmd.Parameters.AddWithValue(name, value);
        return cmd.ExecuteNonQuery();
    }

    private static bool ApplyCase(SqliteConnection conn, HiddenOverride ov, int sid)
    {
        var cn = ov.RecordKey;
        return ov.Field switch
        {
            "Owner" => Exec(conn, "UPDATE Cases SET Owner=@v WHERE CaseNumber=@cn AND SnapshotId=@sid",
                ("@v", ov.Value), ("@cn", cn), ("@sid", sid)) > 0,
            "OwnerMobile" => Exec(conn, "UPDATE Cases SET OwnerMobile=@v WHERE CaseNumber=@cn AND SnapshotId=@sid",
                ("@v", ov.Value), ("@cn", cn), ("@sid", sid)) > 0,
            "Archived" => Exec(conn, "UPDATE Cases SET Archived=@v WHERE CaseNumber=@cn AND SnapshotId=@sid",
                ("@v", ov.Value == "1" || ov.Value.Equals("true", StringComparison.OrdinalIgnoreCase) ? 1 : 0),
                ("@cn", cn), ("@sid", sid)) > 0,
            // The case address lives in Specifications, not Cases.
            "Address" => Exec(conn, $"UPDATE Specifications SET Address=@v WHERE CaseId IN ({CaseIdsSql})",
                ("@v", ov.Value), ("@cn", cn), ("@sid", sid)) > 0,
            _ => false
        };
    }

    private static bool ApplyFee(SqliteConnection conn, HiddenOverride ov, int sid)
    {
        var parts = ov.RecordKey.Split('|', 2);
        if (parts.Length < 2) return false;
        var (cn, stage) = (parts[0], parts[1]);
        return ov.Field switch
        {
            "Amount" => Exec(conn, $"UPDATE Fees SET Amount=@v WHERE CaseId IN ({CaseIdsSql}) AND Stage=@st",
                ("@v", ov.Value), ("@cn", cn), ("@sid", sid), ("@st", stage)) > 0,
            "PayStatus" => Exec(conn, $"UPDATE Fees SET PayStatus=@v WHERE CaseId IN ({CaseIdsSql}) AND Stage=@st",
                ("@v", ov.Value), ("@cn", cn), ("@sid", sid), ("@st", stage)) > 0,
            _ => false
        };
    }

    private static bool ApplyEngineer(SqliteConnection conn, HiddenOverride ov, int sid)
    {
        var parts = ov.RecordKey.Split('|', 2);
        if (parts.Length < 2) return false;
        var (cn, name) = (parts[0], parts[1]);
        return ov.Field switch
        {
            // Name is the key itself: rename the row matched by the old name.
            "Name" => Exec(conn, $"UPDATE Engineers SET Name=@v WHERE CaseId IN ({CaseIdsSql}) AND Name=@old",
                ("@v", ov.Value), ("@cn", cn), ("@sid", sid), ("@old", name)) > 0,
            "Phone" => Exec(conn, $"UPDATE Engineers SET Phone=@v WHERE CaseId IN ({CaseIdsSql}) AND Name=@old",
                ("@v", ov.Value), ("@cn", cn), ("@sid", sid), ("@old", name)) > 0,
            _ => false
        };
    }
}
