using Microsoft.Data.Sqlite;

namespace NezamMonitorPro.Core.Data;

// خواندن/نوشتن جدول‌های دفترچهٔ دفاتر و مهندسین یزد. فقط از اتصالِ
// مشترکِ NezamDatabase و زیر قفلِ همان اتصال (SyncRoot) استفاده
// می‌کند — هیچ اتصال دومی باز نمی‌شود. همهٔ دستورها پارامتری‌اند.
public sealed class DirectoryStore
{
    private readonly NezamDatabase _db;

    public DirectoryStore(NezamDatabase db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    // ---- خواندن ----

    public List<DirectoryOffice> GetAllOffices()
    {
        lock (_db.SyncRoot)
        {
            var list = new List<DirectoryOffice>();
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM directory_offices ORDER BY Name ASC";
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(ReadOffice(r));
            return list;
        }
    }

    public DirectoryOffice? GetOffice(int siteId)
    {
        lock (_db.SyncRoot)
        {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM directory_offices WHERE SiteId=@id";
            cmd.Parameters.AddWithValue("@id", siteId);
            using var r = cmd.ExecuteReader();
            return r.Read() ? ReadOffice(r) : null;
        }
    }

    public DirectoryEngineer? GetEngineer(int siteId)
    {
        lock (_db.SyncRoot)
        {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM directory_engineers WHERE SiteId=@id";
            cmd.Parameters.AddWithValue("@id", siteId);
            using var r = cmd.ExecuteReader();
            return r.Read() ? ReadEngineer(r) : null;
        }
    }

    public List<DirectoryEngineer> GetEngineersForOffice(int officeId)
    {
        lock (_db.SyncRoot)
        {
            var list = new List<DirectoryEngineer>();
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM directory_engineers WHERE OfficeId=@id ORDER BY Name ASC, Family ASC";
            cmd.Parameters.AddWithValue("@id", officeId);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(ReadEngineer(r));
            return list;
        }
    }

    public List<DirectoryEducation> GetEducation(int engineerId)
    {
        lock (_db.SyncRoot)
        {
            var list = new List<DirectoryEducation>();
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM directory_education WHERE EngineerId=@id ORDER BY Id ASC";
            cmd.Parameters.AddWithValue("@id", engineerId);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(ReadEducation(r));
            return list;
        }
    }

    public List<DirectoryLicenceHistory> GetLicenceHistory(int engineerId)
    {
        lock (_db.SyncRoot)
        {
            var list = new List<DirectoryLicenceHistory>();
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "SELECT * FROM directory_licence_history WHERE EngineerId=@id ORDER BY Id ASC";
            cmd.Parameters.AddWithValue("@id", engineerId);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(ReadLicenceHistory(r));
            return list;
        }
    }

    public int GetOfficeCount()
    {
        lock (_db.SyncRoot)
        {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM directory_offices";
            return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
        }
    }

    public int GetEngineerCount()
    {
        lock (_db.SyncRoot)
        {
            using var cmd = _db.Connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM directory_engineers";
            return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
        }
    }

    // ---- نوشتن (برای seeder: جایگزینیِ کاملِ مجموعه‌داده) ----

    /// <summary>هر ۴ جدول دفترچه را خالی می‌کند (اول فرزندها، بعد والدها).</summary>
    public void DeleteAllDirectory()
    {
        lock (_db.SyncRoot)
        {
            using var tx = _db.Connection.BeginTransaction();
            try
            {
                ExecuteNonQuery("DELETE FROM directory_education", tx);
                ExecuteNonQuery("DELETE FROM directory_licence_history", tx);
                ExecuteNonQuery("DELETE FROM directory_offices", tx);
                ExecuteNonQuery("DELETE FROM directory_engineers", tx);
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }

    public void InsertOffices(IReadOnlyList<DirectoryOffice> offices)
    {
        if (offices.Count == 0) return;
        lock (_db.SyncRoot)
        {
            using var tx = _db.Connection.BeginTransaction();
            try
            {
                using var cmd = _db.Connection.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT OR REPLACE INTO directory_offices
                    (SiteId, Name, Licence, Manager, Mobile, Contact, Province, City, Address, Phone, Fax, Hours, DetailUrl, MemberCount, UpdatedAt)
                    VALUES (@id, @name, @lic, @mgr, @mob, @con, @prov, @city, @addr, @phone, @fax, @hours, @url, @members, @upd)
                    """;
                var pId = cmd.Parameters.Add("@id", SqliteType.Integer);
                var pName = cmd.Parameters.Add("@name", SqliteType.Text);
                var pLic = cmd.Parameters.Add("@lic", SqliteType.Text);
                var pMgr = cmd.Parameters.Add("@mgr", SqliteType.Text);
                var pMob = cmd.Parameters.Add("@mob", SqliteType.Text);
                var pCon = cmd.Parameters.Add("@con", SqliteType.Text);
                var pProv = cmd.Parameters.Add("@prov", SqliteType.Text);
                var pCity = cmd.Parameters.Add("@city", SqliteType.Text);
                var pAddr = cmd.Parameters.Add("@addr", SqliteType.Text);
                var pPhone = cmd.Parameters.Add("@phone", SqliteType.Text);
                var pFax = cmd.Parameters.Add("@fax", SqliteType.Text);
                var pHours = cmd.Parameters.Add("@hours", SqliteType.Text);
                var pUrl = cmd.Parameters.Add("@url", SqliteType.Text);
                var pMembers = cmd.Parameters.Add("@members", SqliteType.Integer);
                var pUpd = cmd.Parameters.Add("@upd", SqliteType.Text);
                foreach (var o in offices)
                {
                    pId.Value = o.SiteId;
                    pName.Value = o.Name ?? "";
                    pLic.Value = o.Licence ?? "";
                    pMgr.Value = o.Manager ?? "";
                    pMob.Value = o.Mobile ?? "";
                    pCon.Value = o.Contact ?? "";
                    pProv.Value = o.Province ?? "";
                    pCity.Value = o.City ?? "";
                    pAddr.Value = o.Address ?? "";
                    pPhone.Value = o.Phone ?? "";
                    pFax.Value = o.Fax ?? "";
                    pHours.Value = o.Hours ?? "";
                    pUrl.Value = o.DetailUrl ?? "";
                    pMembers.Value = o.MemberCount;
                    pUpd.Value = o.UpdatedAt ?? "";
                    cmd.ExecuteNonQuery();
                }
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }

    public void InsertEngineers(IReadOnlyList<DirectoryEngineer> engineers)
    {
        if (engineers.Count == 0) return;
        lock (_db.SyncRoot)
        {
            using var tx = _db.Connection.BeginTransaction();
            try
            {
                using var cmd = _db.Connection.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT OR REPLACE INTO directory_engineers
                    (SiteId, Name, Family, Discipline, Phone, DesignGrade, SupervisionGrade, ExecutionGrade, LicenceIssue, LicenceValidity, Photo, OfficeId, OfficeName, UpdatedAt)
                    VALUES (@id, @name, @fam, @disc, @phone, @dg, @sg, @eg, @li, @lv, @photo, @oid, @oname, @upd)
                    """;
                var pId = cmd.Parameters.Add("@id", SqliteType.Integer);
                var pName = cmd.Parameters.Add("@name", SqliteType.Text);
                var pFam = cmd.Parameters.Add("@fam", SqliteType.Text);
                var pDisc = cmd.Parameters.Add("@disc", SqliteType.Text);
                var pPhone = cmd.Parameters.Add("@phone", SqliteType.Text);
                var pDg = cmd.Parameters.Add("@dg", SqliteType.Text);
                var pSg = cmd.Parameters.Add("@sg", SqliteType.Text);
                var pEg = cmd.Parameters.Add("@eg", SqliteType.Text);
                var pLi = cmd.Parameters.Add("@li", SqliteType.Text);
                var pLv = cmd.Parameters.Add("@lv", SqliteType.Text);
                var pPhoto = cmd.Parameters.Add("@photo", SqliteType.Text);
                var pOid = cmd.Parameters.Add("@oid", SqliteType.Integer);
                var pOname = cmd.Parameters.Add("@oname", SqliteType.Text);
                var pUpd = cmd.Parameters.Add("@upd", SqliteType.Text);
                foreach (var e in engineers)
                {
                    pId.Value = e.SiteId;
                    pName.Value = e.Name ?? "";
                    pFam.Value = e.Family ?? "";
                    pDisc.Value = e.Discipline ?? "";
                    pPhone.Value = e.Phone ?? "";
                    pDg.Value = e.DesignGrade ?? "";
                    pSg.Value = e.SupervisionGrade ?? "";
                    pEg.Value = e.ExecutionGrade ?? "";
                    pLi.Value = e.LicenceIssue ?? "";
                    pLv.Value = e.LicenceValidity ?? "";
                    pPhoto.Value = e.Photo ?? "";
                    pOid.Value = e.OfficeId;
                    pOname.Value = e.OfficeName ?? "";
                    pUpd.Value = e.UpdatedAt ?? "";
                    cmd.ExecuteNonQuery();
                }
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }

    public void InsertEducation(IReadOnlyList<DirectoryEducation> rows)
    {
        if (rows.Count == 0) return;
        lock (_db.SyncRoot)
        {
            using var tx = _db.Connection.BeginTransaction();
            try
            {
                using var cmd = _db.Connection.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO directory_education
                    (EngineerId, Degree, Field, Orientation, UniversityType, University, City, Country, Start, End)
                    VALUES (@eid, @deg, @field, @ori, @utype, @uni, @city, @country, @start, @end)
                    """;
                var pEid = cmd.Parameters.Add("@eid", SqliteType.Integer);
                var pDeg = cmd.Parameters.Add("@deg", SqliteType.Text);
                var pField = cmd.Parameters.Add("@field", SqliteType.Text);
                var pOri = cmd.Parameters.Add("@ori", SqliteType.Text);
                var pUtype = cmd.Parameters.Add("@utype", SqliteType.Text);
                var pUni = cmd.Parameters.Add("@uni", SqliteType.Text);
                var pCity = cmd.Parameters.Add("@city", SqliteType.Text);
                var pCountry = cmd.Parameters.Add("@country", SqliteType.Text);
                var pStart = cmd.Parameters.Add("@start", SqliteType.Text);
                var pEnd = cmd.Parameters.Add("@end", SqliteType.Text);
                foreach (var row in rows)
                {
                    pEid.Value = row.EngineerId;
                    pDeg.Value = row.Degree ?? "";
                    pField.Value = row.Field ?? "";
                    pOri.Value = row.Orientation ?? "";
                    pUtype.Value = row.UniversityType ?? "";
                    pUni.Value = row.University ?? "";
                    pCity.Value = row.City ?? "";
                    pCountry.Value = row.Country ?? "";
                    pStart.Value = row.Start ?? "";
                    pEnd.Value = row.End ?? "";
                    cmd.ExecuteNonQuery();
                }
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }

    public void InsertLicenceHistory(IReadOnlyList<DirectoryLicenceHistory> rows)
    {
        if (rows.Count == 0) return;
        lock (_db.SyncRoot)
        {
            using var tx = _db.Connection.BeginTransaction();
            try
            {
                using var cmd = _db.Connection.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = """
                    INSERT INTO directory_licence_history
                    (EngineerId, Kind, Grade1, Grade2, Grade3)
                    VALUES (@eid, @kind, @g1, @g2, @g3)
                    """;
                var pEid = cmd.Parameters.Add("@eid", SqliteType.Integer);
                var pKind = cmd.Parameters.Add("@kind", SqliteType.Text);
                var pG1 = cmd.Parameters.Add("@g1", SqliteType.Text);
                var pG2 = cmd.Parameters.Add("@g2", SqliteType.Text);
                var pG3 = cmd.Parameters.Add("@g3", SqliteType.Text);
                foreach (var row in rows)
                {
                    pEid.Value = row.EngineerId;
                    pKind.Value = row.Kind ?? "";
                    pG1.Value = row.Grade1 ?? "";
                    pG2.Value = row.Grade2 ?? "";
                    pG3.Value = row.Grade3 ?? "";
                    cmd.ExecuteNonQuery();
                }
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }

    // ---- خواندن ردیف‌ها ----

    private static DirectoryOffice ReadOffice(SqliteDataReader r) => new(
        SiteId: SafeInt(r, "SiteId"),
        Name: SafeString(r, "Name"),
        Licence: SafeString(r, "Licence"),
        Manager: SafeString(r, "Manager"),
        Mobile: SafeString(r, "Mobile"),
        Contact: SafeString(r, "Contact"),
        Province: SafeString(r, "Province"),
        City: SafeString(r, "City"),
        Address: SafeString(r, "Address"),
        Phone: SafeString(r, "Phone"),
        Fax: SafeString(r, "Fax"),
        Hours: SafeString(r, "Hours"),
        DetailUrl: SafeString(r, "DetailUrl"),
        MemberCount: SafeInt(r, "MemberCount"),
        UpdatedAt: SafeString(r, "UpdatedAt"));

    private static DirectoryEngineer ReadEngineer(SqliteDataReader r) => new(
        SiteId: SafeInt(r, "SiteId"),
        Name: SafeString(r, "Name"),
        Family: SafeString(r, "Family"),
        Discipline: SafeString(r, "Discipline"),
        Phone: SafeString(r, "Phone"),
        DesignGrade: SafeString(r, "DesignGrade"),
        SupervisionGrade: SafeString(r, "SupervisionGrade"),
        ExecutionGrade: SafeString(r, "ExecutionGrade"),
        LicenceIssue: SafeString(r, "LicenceIssue"),
        LicenceValidity: SafeString(r, "LicenceValidity"),
        Photo: SafeString(r, "Photo"),
        OfficeId: SafeInt(r, "OfficeId"),
        OfficeName: SafeString(r, "OfficeName"),
        UpdatedAt: SafeString(r, "UpdatedAt"));

    private static DirectoryEducation ReadEducation(SqliteDataReader r) => new(
        Id: SafeLong(r, "Id"),
        EngineerId: SafeInt(r, "EngineerId"),
        Degree: SafeString(r, "Degree"),
        Field: SafeString(r, "Field"),
        Orientation: SafeString(r, "Orientation"),
        UniversityType: SafeString(r, "UniversityType"),
        University: SafeString(r, "University"),
        City: SafeString(r, "City"),
        Country: SafeString(r, "Country"),
        Start: SafeString(r, "Start"),
        End: SafeString(r, "End"));

    private static DirectoryLicenceHistory ReadLicenceHistory(SqliteDataReader r) => new(
        Id: SafeLong(r, "Id"),
        EngineerId: SafeInt(r, "EngineerId"),
        Kind: SafeString(r, "Kind"),
        Grade1: SafeString(r, "Grade1"),
        Grade2: SafeString(r, "Grade2"),
        Grade3: SafeString(r, "Grade3"));

    private static string SafeString(SqliteDataReader r, string column)
    {
        try
        {
            var idx = r.GetOrdinal(column);
            return r.IsDBNull(idx) ? "" : r.GetString(idx);
        }
        catch { return ""; }
    }

    private static int SafeInt(SqliteDataReader r, string column)
    {
        try
        {
            var idx = r.GetOrdinal(column);
            return r.IsDBNull(idx) ? 0 : r.GetInt32(idx);
        }
        catch { return 0; }
    }

    private static long SafeLong(SqliteDataReader r, string column)
    {
        try
        {
            var idx = r.GetOrdinal(column);
            return r.IsDBNull(idx) ? 0 : r.GetInt64(idx);
        }
        catch { return 0; }
    }

    private void ExecuteNonQuery(string sql, SqliteTransaction tx)
    {
        using var cmd = _db.Connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
