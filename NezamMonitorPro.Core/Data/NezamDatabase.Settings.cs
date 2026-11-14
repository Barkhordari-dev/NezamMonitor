using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using NezamMonitorPro.Core.Models;

namespace NezamMonitorPro.Core.Data;

public sealed partial class NezamDatabase
{
    public string GetSetting(string key, string defaultValue = "")
    {
        lock (_syncRoot)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT Value FROM Settings WHERE Key=@k";
            cmd.Parameters.AddWithValue("@k", key);
            var result = cmd.ExecuteScalar();
            return result?.ToString() ?? defaultValue;
        }
    }

    public Dictionary<string, string> LoadSettings()
    {
        lock (_syncRoot)
        {
            var settings = new Dictionary<string, string>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT Key, Value FROM Settings";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                settings[r.GetString(0)] = r.GetString(1);
            }
            return settings;
        }
    }

    public void SaveSetting(string key, string value)
    {
        lock (_syncRoot)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO Settings (Key, Value) VALUES (@k, @v)";
            cmd.Parameters.AddWithValue("@k", key);
            cmd.Parameters.AddWithValue("@v", value);
            cmd.ExecuteNonQuery();
        }
    }

    // ── Hidden menu overrides ──────────────────────────────────────────

    public List<HiddenOverride> GetHiddenOverrides()
    {
        lock (_syncRoot)
        {
            var list = new List<HiddenOverride>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT Id, RecordType, RecordKey, Field, Value, Mode, UpdatedAt FROM HiddenOverrides ORDER BY RecordType, RecordKey, Field";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new HiddenOverride
                {
                    Id = r.GetInt64(0),
                    RecordType = SafeGetString(r, "RecordType"),
                    RecordKey = SafeGetString(r, "RecordKey"),
                    Field = SafeGetString(r, "Field"),
                    Value = SafeGetString(r, "Value"),
                    Mode = SafeGetString(r, "Mode"),
                    UpdatedAt = SafeGetString(r, "UpdatedAt")
                });
            }
            return list;
        }
    }

    public void SaveHiddenOverride(HiddenOverride ov)
    {
        lock (_syncRoot)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO HiddenOverrides (RecordType, RecordKey, Field, Value, Mode, UpdatedAt)
                VALUES (@t, @k, @f, @v, @m, @u)
                ON CONFLICT(RecordType, RecordKey, Field) DO UPDATE SET Value=excluded.Value, Mode=excluded.Mode, UpdatedAt=excluded.UpdatedAt
                """;
            cmd.Parameters.AddWithValue("@t", ov.RecordType);
            cmd.Parameters.AddWithValue("@k", ov.RecordKey);
            cmd.Parameters.AddWithValue("@f", ov.Field);
            cmd.Parameters.AddWithValue("@v", ov.Value);
            cmd.Parameters.AddWithValue("@m", ov.Mode);
            cmd.Parameters.AddWithValue("@u", ov.UpdatedAt);
            cmd.ExecuteNonQuery();
        }
    }

    public void DeleteHiddenOverride(long id)
    {
        lock (_syncRoot)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM HiddenOverrides WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }

    public void DeleteTempOverrides()
    {
        lock (_syncRoot)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM HiddenOverrides WHERE Mode='temp'";
            cmd.ExecuteNonQuery();
        }
    }

    public string GetTheme()
    {
        return GetSetting(SettingKeys.Theme, "");
    }

    public void SaveTheme(string json)
    {
        SaveSetting(SettingKeys.Theme, json);
    }
}
