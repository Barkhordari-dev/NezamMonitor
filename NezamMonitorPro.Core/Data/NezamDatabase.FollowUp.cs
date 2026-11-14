using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using NezamMonitorPro.Core.Models;

namespace NezamMonitorPro.Core.Data;

public sealed partial class NezamDatabase
{
    // ========== FollowUpEdits Methods ==========

    /// <summary>
    /// دریافت ویرایش‌های پیگیری برای یک پرونده
    /// </summary>
    public (string Description, string CaseNumberEdit, string OwnerEdit, string AddressEdit, string OwnerMobileEdit)? GetFollowUpEdit(string caseNumber)
    {
        lock (_syncRoot)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT Description, CaseNumberEdit, OwnerEdit, AddressEdit, OwnerMobileEdit FROM FollowUpEdits WHERE CaseNumber=@cn";
            cmd.Parameters.AddWithValue("@cn", caseNumber);
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                return (
                    r.GetString(0),
                    r.GetString(1),
                    r.GetString(2),
                    r.GetString(3),
                    r.GetString(4)
                );
            }
            return null;
        }
    }

    /// <summary>
    /// دریافت تمام ویرایش‌های پیگیری
    /// </summary>
    public Dictionary<string, FollowUpEditRow> GetAllFollowUpEdits()
    {
        lock (_syncRoot)
        {
            var result = new Dictionary<string, FollowUpEditRow>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT CaseNumber, Description, CaseNumberEdit, OwnerEdit, AddressEdit, OwnerMobileEdit, CreatedAt, UpdatedAt FROM FollowUpEdits";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                result[r.GetString(0)] = new FollowUpEditRow(r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6), r.GetString(7));
            }
            return result;
        }
    }

    /// <summary>
    /// ذخیره ویرایش‌های پیگیری
    /// </summary>
    public void SaveFollowUpEdit(string caseNumber, string description, string caseNumberEdit, string ownerEdit, string addressEdit, string ownerMobileEdit)
    {
        lock (_syncRoot)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT OR REPLACE INTO FollowUpEdits (CaseNumber, Description, CaseNumberEdit, OwnerEdit, AddressEdit, OwnerMobileEdit, CreatedAt, UpdatedAt)
                VALUES (@cn, @desc, @cnEdit, @ownerEdit, @addrEdit, @mobileEdit, COALESCE((SELECT CreatedAt FROM FollowUpEdits WHERE CaseNumber=@cn), @now), @now)";
            cmd.Parameters.AddWithValue("@cn", caseNumber);
            cmd.Parameters.AddWithValue("@desc", description);
            cmd.Parameters.AddWithValue("@cnEdit", caseNumberEdit);
            cmd.Parameters.AddWithValue("@ownerEdit", ownerEdit);
            cmd.Parameters.AddWithValue("@addrEdit", addressEdit);
            cmd.Parameters.AddWithValue("@mobileEdit", ownerMobileEdit);
            cmd.Parameters.AddWithValue("@now", DateTime.Now.ToString("yyyy/MM/dd HH:mm"));
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// حذف ویرایش‌های پیگیری
    /// </summary>
    public void DeleteFollowUpEdit(string caseNumber)
    {
        lock (_syncRoot)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM FollowUpEdits WHERE CaseNumber=@cn";
            cmd.Parameters.AddWithValue("@cn", caseNumber);
            cmd.ExecuteNonQuery();
        }
    }
}
