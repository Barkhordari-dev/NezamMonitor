using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using NezamMonitorPro.Core.Models;

namespace NezamMonitorPro.Core.Data;

public sealed partial class NezamDatabase
{
    public long CreateSnapshot(DateTime now)
    {
        lock (_syncRoot)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT INTO Snapshots (CreatedAt, Status) VALUES (@t, 'pending'); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@t", now.ToString("O"));
            return Convert.ToInt64(cmd.ExecuteScalar() ?? 0);
        }
    }

    public void FinalizeSnapshot(long id, int caseCount)
    {
        lock (_syncRoot)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "UPDATE Snapshots SET Status='done', CaseCount=@c WHERE Id=@id";
            cmd.Parameters.AddWithValue("@c", caseCount);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }
    }

    public long GetLastValidSnapshotId()
    {
        lock (_syncRoot)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT Id FROM Snapshots WHERE Status='done' ORDER BY Id DESC LIMIT 1";
            return Convert.ToInt64(cmd.ExecuteScalar() ?? 0);
        }
    }

    public int GetActiveSnapshotId()
    {
        lock (_syncRoot)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT SnapshotId FROM ActiveSnapshot WHERE Id=1";
            var result = cmd.ExecuteScalar();
            var sid = result != null ? Convert.ToInt32(result) : 0;

            // Fallback: if no active snapshot was ever set, fall back to the newest
            // completed snapshot so the dashboard/history still show real data.
            if (sid == 0)
            {
                var fallback = GetLastValidSnapshotId();
                if (fallback > 0) return (int)fallback;
            }
            return sid;
        }
    }

    public void SetActiveSnapshot(long snapshotId)
    {
        lock (_syncRoot)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO ActiveSnapshot (Id, SnapshotId) VALUES (1, @sid)";
            cmd.Parameters.AddWithValue("@sid", snapshotId);
            cmd.ExecuteNonQuery();
        }
    }

    public List<(string CaseNumber, string Owner, string Field, string OldValue, string NewValue)> GetChanges(long snapshotId)
    {
        lock (_syncRoot)
        {
            var list = new List<(string, string, string, string, string)>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT CaseNumber, Owner, FieldName, OldValue, NewValue FROM Changes WHERE SnapshotId=@sid";
            cmd.Parameters.AddWithValue("@sid", snapshotId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add((r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4)));
            return list;
        }
    }

    public void SaveChanges(long snapshotId, IReadOnlyList<Core.Diff.FieldChange> changes)
    {
        lock (_syncRoot)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "INSERT INTO Changes (SnapshotId, CaseNumber, Owner, ChangeType, FieldName, OldValue, NewValue) VALUES (@sid, @cn, @owner, @ct, @fn, @ov, @nv)";
            cmd.Parameters.AddWithValue("@sid", snapshotId);
            cmd.Parameters.AddWithValue("@cn", "");
            cmd.Parameters.AddWithValue("@owner", "");
            cmd.Parameters.AddWithValue("@ct", "");
            cmd.Parameters.AddWithValue("@fn", "");
            cmd.Parameters.AddWithValue("@ov", "");
            cmd.Parameters.AddWithValue("@nv", "");
        
            foreach (var ch in changes)
            {
                cmd.Parameters["@cn"].Value = ch.CaseNumber;
                cmd.Parameters["@owner"].Value = ch.Owner;
                cmd.Parameters["@ct"].Value = ch.Type.ToString();
                cmd.Parameters["@fn"].Value = ch.Field;
                cmd.Parameters["@ov"].Value = ch.OldValue;
                cmd.Parameters["@nv"].Value = ch.NewValue;
                cmd.ExecuteNonQuery();
            }
        }
    }

    // ========== HistoryViewModel Methods ==========

    /// <summary>
    /// دریافت تمام اسنپشات‌ها
    /// </summary>
    public List<(long Id, string CreatedAt, int CaseCount, string Status)> GetSnapshots()
    {
        lock (_syncRoot)
        {
            var list = new List<(long, string, int, string)>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT Id, CreatedAt, CaseCount, Status FROM Snapshots ORDER BY Id DESC";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add((
                    r.GetInt64(0),
                    r.GetString(1),
                    r.GetInt32(2),
                    r.GetString(3)
                ));
            }
            return list;
        }
    }

    /// <summary>
    /// حذف اسنپشات
    /// </summary>
    public void DeleteSnapshot(long snapshotId)
    {
        lock (_syncRoot)
        {
            // در یک تراکنش: اول فرزندها، بعد خود اسنپ‌شات (با FK روشن،
            // حذف والد پیش از فرزند خطا می‌دهد)
            using var transaction = _conn.BeginTransaction();
            try
            {
                Execute($"DELETE FROM Changes WHERE SnapshotId = {snapshotId}", transaction);
                Execute($"DELETE FROM Specifications WHERE CaseId IN (SELECT Id FROM Cases WHERE SnapshotId = {snapshotId})", transaction);
                Execute($"DELETE FROM Engineers WHERE CaseId IN (SELECT Id FROM Cases WHERE SnapshotId = {snapshotId})", transaction);
                Execute($"DELETE FROM Fees WHERE CaseId IN (SELECT Id FROM Cases WHERE SnapshotId = {snapshotId})", transaction);
                Execute($"DELETE FROM Reports WHERE CaseId IN (SELECT Id FROM Cases WHERE SnapshotId = {snapshotId})", transaction);
                Execute($"DELETE FROM Cases WHERE SnapshotId = {snapshotId}", transaction);
                Execute($"DELETE FROM Snapshots WHERE Id = {snapshotId}", transaction);
                // اگر اسنپ‌شات فعال همین بود، اشاره‌گر فعال را هم بردار
                Execute($"DELETE FROM ActiveSnapshot WHERE SnapshotId = {snapshotId}", transaction);
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }

    /// <summary>
        /// ادغام اسنپشات‌ها (برای HistoryViewModel)
        /// </summary>
        /// <summary>
        /// ادغام هوشمند اسنپ‌شات‌ها. 
        /// پرونده‌های با شماره یکسان با هم ترکیب می‌شوند (فیلدهای خالی با داده‌های جدید پر می‌شوند).
        /// </summary>
        public long MergeSnapshots(List<long> snapshotIds)
        {
            lock (_syncRoot)
            {
                if (snapshotIds.Count < 2) return 0;

                using var transaction = _conn.BeginTransaction();
                try
                {
                    var targetId = snapshotIds[0];
                    var others = snapshotIds.Skip(1).ToList();

                    foreach (var sourceId in others)
                    {
                        // ۱. ادغام فیلدهای پرونده‌های مشترک (فقط فیلدهایی که در مقصد خالی هستند را از منبع می‌گیرد)
                        // این دستور SQL به صورت هوشمند فیلدها را مقایسه و بروزرسانی می‌کند
                        Execute($@"
                            UPDATE Cases 
                            SET 
                                Owner = CASE WHEN (Owner IS NULL OR Owner = '') THEN (SELECT Owner FROM Cases c2 WHERE c2.CaseNumber = Cases.CaseNumber AND c2.SnapshotId = {sourceId} LIMIT 1) ELSE Owner END,
                                OwnerMobile = CASE WHEN (OwnerMobile IS NULL OR OwnerMobile = '') THEN (SELECT OwnerMobile FROM Cases c2 WHERE c2.CaseNumber = Cases.CaseNumber AND c2.SnapshotId = {sourceId} LIMIT 1) ELSE OwnerMobile END,
                                OwnerAddress = CASE WHEN (OwnerAddress IS NULL OR OwnerAddress = '') THEN (SELECT OwnerAddress FROM Cases c2 WHERE c2.CaseNumber = Cases.CaseNumber AND c2.SnapshotId = {sourceId} LIMIT 1) ELSE OwnerAddress END,
                                Responsibility = CASE WHEN (Responsibility IS NULL OR Responsibility = '') THEN (SELECT Responsibility FROM Cases c2 WHERE c2.CaseNumber = Cases.CaseNumber AND c2.SnapshotId = {sourceId} LIMIT 1) ELSE Responsibility END
                            WHERE SnapshotId = {targetId} 
                            AND EXISTS (SELECT 1 FROM Cases c2 WHERE c2.CaseNumber = Cases.CaseNumber AND c2.SnapshotId = {sourceId})", transaction);

                        // ۲. انتقال پرونده‌هایی که در مقصد وجود ندارند
                        Execute($@"
                            UPDATE Cases SET SnapshotId = {targetId} 
                            WHERE SnapshotId = {sourceId} 
                            AND CaseNumber NOT IN (SELECT CaseNumber FROM Cases WHERE SnapshotId = {targetId})", transaction);

                        // ۳. انتقال رکوردهای وابسته (مشخصات، مهندسین و ...) برای پرونده‌های جدید منتقل شده
                        // (پرونده‌هایی که SnapshotId آن‌ها در مرحله قبل به targetId تغییر کرد)
                    
                        // ۴. حذف اسنپ‌شات منبع و پرونده‌های باقی‌مانده (تکراری‌ها)؛
                        // اول فرزندهای پرونده‌های تکراری، بعد خود پرونده‌ها، بعد
                        // ردیف‌های Changes آن اسنپ‌شات (با FK روشن، حذف والد
                        // پیش از فرزند خطا می‌دهد)
                        Execute($"DELETE FROM Specifications WHERE CaseId IN (SELECT Id FROM Cases WHERE SnapshotId = {sourceId})", transaction);
                        Execute($"DELETE FROM Engineers WHERE CaseId IN (SELECT Id FROM Cases WHERE SnapshotId = {sourceId})", transaction);
                        Execute($"DELETE FROM Fees WHERE CaseId IN (SELECT Id FROM Cases WHERE SnapshotId = {sourceId})", transaction);
                        Execute($"DELETE FROM Reports WHERE CaseId IN (SELECT Id FROM Cases WHERE SnapshotId = {sourceId})", transaction);
                        Execute($"DELETE FROM Cases WHERE SnapshotId = {sourceId}", transaction);
                        Execute($"DELETE FROM Changes WHERE SnapshotId = {sourceId}", transaction);
                        Execute($"DELETE FROM Snapshots WHERE Id = {sourceId}", transaction);
                        // اگر اسنپ‌شات فعال یکی از ادغام‌شده‌ها بود، به مقصد اشاره کند
                        Execute($"UPDATE ActiveSnapshot SET SnapshotId = {targetId} WHERE SnapshotId = {sourceId}", transaction);
                    }

                    // ۵. به‌روزرسانی تعداد نهایی
                    Execute($"UPDATE Snapshots SET CaseCount = (SELECT COUNT(*) FROM Cases WHERE SnapshotId = {targetId}) WHERE Id = {targetId}", transaction);

                    transaction.Commit();
                    return targetId;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }
}
