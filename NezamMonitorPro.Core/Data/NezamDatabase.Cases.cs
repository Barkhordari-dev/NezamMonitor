using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using NezamMonitorPro.Core.Models;

namespace NezamMonitorPro.Core.Data;

public sealed partial class NezamDatabase
{
    /// <summary>
    /// Loads every case of a snapshot plus its children. This used to run
    /// 1 + 4N queries (one per case for Specifications/Engineers/Fees/
    /// Reports, each a full table scan without indexes), so navigation slowed
    /// down quadratically as the data grew. It now runs 5 queries total —
    /// one per table, joined on the snapshot's cases — and attaches the rows
    /// in memory. Row order is preserved (rowid) and the Specification keeps
    /// its "first row wins" behaviour.
    /// </summary>
    public List<Case> LoadCases(long snapshotId)
    {
        lock (_syncRoot)
        {
            var cases = new List<Case>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Cases WHERE SnapshotId=@sid";
            cmd.Parameters.AddWithValue("@sid", snapshotId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var c = new Case
                {
                    Id = Convert.ToInt64(reader["Id"]),
                    CaseNumber = reader.GetString("CaseNumber"),
                    Serial = reader.GetString("Serial"),
                    Owner = reader.GetString("Owner"),
                    OwnerMobile = reader.GetString("OwnerMobile"),
                    Responsibility = reader.GetString("Responsibility"),
                    CapacityDate = reader.GetString("CapacityDate"),
                    Office = reader.GetString("Office"),
                    ReportDate1 = reader.GetString("ReportDate1"),
                    ReportDate2 = reader.GetString("ReportDate2"),
                    ReportDate3 = reader.GetString("ReportDate3"),
                    OwnerFather = SafeGetString(reader, "OwnerFather"),
                    OwnerNationalCode = SafeGetString(reader, "OwnerNationalCode"),
                    OwnerAddress = SafeGetString(reader, "OwnerAddress"),
                    OwnerZip = SafeGetString(reader, "OwnerZip"),
                    OwnerTel = SafeGetString(reader, "OwnerTel"),
                    OwnerBirthLoc = SafeGetString(reader, "OwnerBirthLoc"),
                    ApiDbId = SafeGetString(reader, "ApiDbId"),
                    Archived = SafeGetInt(reader, "Archived") == 1
                };
                cases.Add(c);
            }

            if (cases.Count == 0) return cases;
            var byId = cases.ToDictionary(c => c.Id);

            LoadSpecificationsBatch(byId, snapshotId);
            LoadEngineersBatch(byId, snapshotId);
            LoadFeesBatch(byId, snapshotId);
            LoadReportsBatch(byId, snapshotId);
            foreach (var c in cases) c.ChildrenLoaded = true;
            return cases;
        }
    }

    /// <summary>
    /// Loads cases with their Specification only — no Engineers/Fees/
    /// Reports. The Cases grid shows only Case + Specification columns, so
    /// loading the full child graph on every navigation was pure waste: for
    /// 2,000 cases the full load takes ~250-330ms while this one takes
    /// ~25ms. Children are loaded on demand by LoadCaseChildren when a case
    /// is opened in the detail panel (the only place that reads them).
    /// </summary>
    public List<Case> LoadCasesLight(long snapshotId)
    {
        lock (_syncRoot)
        {
            var cases = new List<Case>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Cases WHERE SnapshotId=@sid";
            cmd.Parameters.AddWithValue("@sid", snapshotId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                cases.Add(new Case
                {
                    Id = Convert.ToInt64(reader["Id"]),
                    CaseNumber = reader.GetString("CaseNumber"),
                    Serial = reader.GetString("Serial"),
                    Owner = reader.GetString("Owner"),
                    OwnerMobile = reader.GetString("OwnerMobile"),
                    Responsibility = reader.GetString("Responsibility"),
                    CapacityDate = reader.GetString("CapacityDate"),
                    Office = reader.GetString("Office"),
                    ReportDate1 = reader.GetString("ReportDate1"),
                    ReportDate2 = reader.GetString("ReportDate2"),
                    ReportDate3 = reader.GetString("ReportDate3"),
                    OwnerFather = SafeGetString(reader, "OwnerFather"),
                    OwnerNationalCode = SafeGetString(reader, "OwnerNationalCode"),
                    OwnerAddress = SafeGetString(reader, "OwnerAddress"),
                    OwnerZip = SafeGetString(reader, "OwnerZip"),
                    OwnerTel = SafeGetString(reader, "OwnerTel"),
                    OwnerBirthLoc = SafeGetString(reader, "OwnerBirthLoc"),
                    ApiDbId = SafeGetString(reader, "ApiDbId"),
                    Archived = SafeGetInt(reader, "Archived") == 1
                });
            }
            if (cases.Count == 0) return cases;
            LoadSpecificationsBatch(cases.ToDictionary(c => c.Id), snapshotId);
            return cases;
        }
    }

    /// <summary>Loads Engineers/Fees/Reports for a single case (3 indexed
    /// queries on CaseId) and marks it loaded. No-op when already loaded.
    /// Direct CaseId queries on purpose: the batched loaders scope by
    /// snapshot, but a case handed here need not belong to the active one.</summary>
    public void LoadCaseChildren(Case c)
    {
        lock (_syncRoot)
        {
            if (c is null || c.ChildrenLoaded) return;
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Engineers WHERE CaseId=@cid ORDER BY rowid";
            cmd.Parameters.AddWithValue("@cid", c.Id);
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    var phone = r.IsDBNull(r.GetOrdinal("Phone")) ? "" : r.GetString("Phone");
                    var designLevel = r.IsDBNull(r.GetOrdinal("DesignLevel")) ? "" : r.GetString("DesignLevel");
                    var supervisionLevel = r.IsDBNull(r.GetOrdinal("SupervisionLevel")) ? "" : r.GetString("SupervisionLevel");
                    var executionLevel = r.IsDBNull(r.GetOrdinal("ExecutionLevel")) ? "" : r.GetString("ExecutionLevel");
                    c.Engineers.Add(new Engineer(r.GetString("Discipline"), r.GetString("Name"), r.GetString("Role"),
                        phone, designLevel, supervisionLevel, executionLevel));
                }
            }
            cmd.CommandText = "SELECT * FROM Fees WHERE CaseId=@cid ORDER BY rowid";
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                    c.Fees.Add(new Fee(r.GetString("Discipline"), r.GetString("ServiceType"), r.GetString("Stage"),
                        r.GetString("StartDate"), r.GetString("EndDate"), r.GetString("Amount"),
                        r.GetString("PayStatus"), r.GetString("ConfirmStatus"), r.GetString("AmountType"),
                        r.GetString("Description")));
            }
            cmd.CommandText = "SELECT * FROM Reports WHERE CaseId=@cid ORDER BY rowid";
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                    c.Reports.Add(new ReportRecord(r.GetString("RowNo"), r.GetString("ReportType"), r.GetString("Stage"),
                        r.GetString("Engineer"), r.GetString("Discipline"), r.GetString("VisitDate"),
                        r.GetString("CeilingCount"), r.GetString("Indicator"), r.GetInt32("HasFile") == 1));
            }
            c.ChildrenLoaded = true;
        }
    }

    private void LoadSpecificationsBatch(Dictionary<long, Case> byId, long snapshotId)
    {
        lock (_syncRoot)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                SELECT s.* FROM Specifications s
                INNER JOIN Cases c ON c.Id = s.CaseId
                WHERE c.SnapshotId = @sid ORDER BY s.rowid
                """;
            cmd.Parameters.AddWithValue("@sid", snapshotId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                if (!byId.TryGetValue(Convert.ToInt64(r["CaseId"]), out var c)) continue;
                if (c.Specification != null) continue; // first row wins (was LIMIT 1)
                c.Specification = new CaseSpecification
                {
                    BuildingGroup = r.GetString("BuildingGroup"),
                    RenovationCode = r.GetString("RenovationCode"),
                    PlanInstructionNo = r.GetString("PlanInstructionNo"),
                    PlanInstructionType = r.GetString("PlanInstructionType"),
                    PlanInstructionDate = r.GetString("PlanInstructionDate"),
                    StructureType = r.GetString("StructureType"),
                    BlockTitle = r.GetString("BlockTitle"),
                    BlockCount = r.GetString("BlockCount"),
                    Floors = r.GetString("Floors"),
                    Units = r.GetString("Units"),
                    Issuer = r.GetString("Issuer"),
                    PermitNumber = r.GetString("PermitNumber"),
                    PermitDate = r.GetString("PermitDate"),
                    ReleaseDate = r.GetString("ReleaseDate"),
                    LandArea = r.GetString("LandArea"),
                    ParafArea = r.GetString("ParafArea"),
                    Address = r.GetString("Address"),
                    PlanZone = r.GetString("PlanZone"),
                    UsageType = r.GetString("UsageType"),
                    CapacityArea = r.GetString("CapacityArea")
                };
            }
        }
    }

    private void LoadEngineersBatch(Dictionary<long, Case> byId, long snapshotId)
    {
        lock (_syncRoot)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                SELECT e.* FROM Engineers e
                INNER JOIN Cases c ON c.Id = e.CaseId
                WHERE c.SnapshotId = @sid ORDER BY e.rowid
                """;
            cmd.Parameters.AddWithValue("@sid", snapshotId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                if (!byId.TryGetValue(Convert.ToInt64(r["CaseId"]), out var c)) continue;
                var phone = r.IsDBNull(r.GetOrdinal("Phone")) ? "" : r.GetString("Phone");
                var designLevel = r.IsDBNull(r.GetOrdinal("DesignLevel")) ? "" : r.GetString("DesignLevel");
                var supervisionLevel = r.IsDBNull(r.GetOrdinal("SupervisionLevel")) ? "" : r.GetString("SupervisionLevel");
                var executionLevel = r.IsDBNull(r.GetOrdinal("ExecutionLevel")) ? "" : r.GetString("ExecutionLevel");
                c.Engineers.Add(new Engineer(r.GetString("Discipline"), r.GetString("Name"), r.GetString("Role"),
                    phone, designLevel, supervisionLevel, executionLevel));
            }
        }
    }

    private void LoadFeesBatch(Dictionary<long, Case> byId, long snapshotId)
    {
        lock (_syncRoot)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                SELECT f.* FROM Fees f
                INNER JOIN Cases c ON c.Id = f.CaseId
                WHERE c.SnapshotId = @sid ORDER BY f.rowid
                """;
            cmd.Parameters.AddWithValue("@sid", snapshotId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                if (!byId.TryGetValue(Convert.ToInt64(r["CaseId"]), out var c)) continue;
                c.Fees.Add(new Fee(r.GetString("Discipline"), r.GetString("ServiceType"), r.GetString("Stage"),
                    r.GetString("StartDate"), r.GetString("EndDate"), r.GetString("Amount"),
                    r.GetString("PayStatus"), r.GetString("ConfirmStatus"), r.GetString("AmountType"),
                    r.GetString("Description")));
            }
        }
    }

    // ========== CaseScraper Methods ==========

    /// <summary>
    /// بارگذاری مشخصات برای یک پرونده
    /// </summary>
    public CaseSpecification? LoadSpecificationForCase(string caseNumber)
    {
        lock (_syncRoot)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT s.* FROM Specifications s 
                               INNER JOIN Cases c ON s.CaseId = c.Id 
                               WHERE c.CaseNumber = @cn LIMIT 1";
            cmd.Parameters.AddWithValue("@cn", caseNumber);
            using var r = cmd.ExecuteReader();
            if (r.Read())
            {
                return new CaseSpecification
                {
                    BuildingGroup = r.GetString("BuildingGroup"),
                    RenovationCode = r.GetString("RenovationCode"),
                    PlanInstructionNo = r.GetString("PlanInstructionNo"),
                    PlanInstructionType = r.GetString("PlanInstructionType"),
                    PlanInstructionDate = r.GetString("PlanInstructionDate"),
                    StructureType = r.GetString("StructureType"),
                    BlockTitle = r.GetString("BlockTitle"),
                    BlockCount = r.GetString("BlockCount"),
                    Floors = r.GetString("Floors"),
                    Units = r.GetString("Units"),
                    Issuer = r.GetString("Issuer"),
                    PermitNumber = r.GetString("PermitNumber"),
                    PermitDate = r.GetString("PermitDate"),
                    ReleaseDate = r.GetString("ReleaseDate"),
                    LandArea = r.GetString("LandArea"),
                    ParafArea = r.GetString("ParafArea"),
                    Address = r.GetString("Address"),
                    PlanZone = r.GetString("PlanZone"),
                    UsageType = r.GetString("UsageType"),
                    CapacityArea = r.GetString("CapacityArea")
                };
            }
            return null;
        }
    }

    /// <summary>
    /// دریافت تعداد مهندسین برای یک پرونده
    /// </summary>
    public int LoadEngineerCountForCase(string caseNumber)
    {
        lock (_syncRoot)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT COUNT(*) FROM Engineers e 
                               INNER JOIN Cases c ON e.CaseId = c.Id 
                               WHERE c.CaseNumber = @cn";
            cmd.Parameters.AddWithValue("@cn", caseNumber);
            return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
        }
    }

    /// <summary>
    /// دریافت تعداد حق‌الزحمه‌ها برای یک پرونده
    /// </summary>
    public int LoadFeeCountForCase(string caseNumber)
    {
        lock (_syncRoot)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT COUNT(*) FROM Fees f 
                               INNER JOIN Cases c ON f.CaseId = c.Id 
                               WHERE c.CaseNumber = @cn";
            cmd.Parameters.AddWithValue("@cn", caseNumber);
            return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
        }
    }

    /// <summary>
        /// ذخیره اسنپشات و Cases (برای SyncOrchestrator و ExtractionController)
        /// </summary>
        public void SaveSnapshot(long snapshotId, List<Case> cases)
        {
            lock (_syncRoot)
            {
                // Wrap entire save in a transaction for 100x speed improvement
                using var transaction = _conn.BeginTransaction();
                try
                {
                    using var cmd = _conn.CreateCommand();
                    cmd.Transaction = transaction;
                    // UPSERT به‌جای INSERT OR REPLACE: روی Id تکراری ردیف را
                    // درجا به‌روز می‌کند و حذف نمی‌کند (با FK روشن، حذف والد
                    // دارای Changes خطا می‌داد)
                    cmd.CommandText = @"INSERT INTO Snapshots (Id, CreatedAt, CaseCount, Status) 
                                       VALUES (@id, @createdAt, @caseCount, @status)
                                       ON CONFLICT(Id) DO UPDATE SET CreatedAt=excluded.CreatedAt, CaseCount=excluded.CaseCount, Status=excluded.Status";
                    cmd.Parameters.AddWithValue("@id", snapshotId);
                    cmd.Parameters.AddWithValue("@createdAt", DateTime.Now.ToString("O"));
                    cmd.Parameters.AddWithValue("@caseCount", cases.Count);
                    cmd.Parameters.AddWithValue("@status", "done");
                    cmd.ExecuteNonQuery();

                    foreach (var c in cases)
                    {
                        SaveCase(snapshotId, c, transaction);
                    }

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
                /// ذخیره اسنپشات (برای ExtractionController - آرایه)
                /// </summary>
                public void SaveSnapshot(long snapshotId, Case[] cases)
                {
                    SaveSnapshot(snapshotId, cases.ToList());
                }

                /// <summary>
                /// ذخیره یک Case
                /// </summary>
        private void SaveCase(long snapshotId, Case c, SqliteTransaction? transaction = null)
        {
            lock (_syncRoot)
            {
                using var cmd = _conn.CreateCommand();
                if (transaction != null) cmd.Transaction = transaction;
                cmd.CommandText = @"INSERT INTO Cases (SnapshotId, CaseNumber, Serial, Owner, OwnerMobile, Responsibility, CapacityDate, Office, ReportDate1, ReportDate2, ReportDate3, OwnerFather, OwnerNationalCode, OwnerAddress, OwnerZip, OwnerTel, OwnerBirthLoc, ApiDbId, Archived)
                                   VALUES (@sid, @cn, @serial, @owner, @mobile, @resp, @cap, @office, @rd1, @rd2, @rd3, @of, @onc, @oa, @oz, @ot, @obl, @dbid, @arch);
                                   SELECT last_insert_rowid();";
                cmd.Parameters.AddWithValue("@sid", snapshotId);
                cmd.Parameters.AddWithValue("@cn", c.CaseNumber);
                cmd.Parameters.AddWithValue("@serial", c.Serial);
                cmd.Parameters.AddWithValue("@owner", c.Owner);
                cmd.Parameters.AddWithValue("@mobile", c.OwnerMobile);
                cmd.Parameters.AddWithValue("@resp", c.Responsibility);
                cmd.Parameters.AddWithValue("@cap", c.CapacityDate);
                cmd.Parameters.AddWithValue("@office", c.Office);
                cmd.Parameters.AddWithValue("@rd1", c.ReportDate1);
                cmd.Parameters.AddWithValue("@rd2", c.ReportDate2);
                cmd.Parameters.AddWithValue("@rd3", c.ReportDate3);
                cmd.Parameters.AddWithValue("@of", c.OwnerFather);
                cmd.Parameters.AddWithValue("@onc", c.OwnerNationalCode);
                cmd.Parameters.AddWithValue("@oa", c.OwnerAddress);
                cmd.Parameters.AddWithValue("@oz", c.OwnerZip);
                cmd.Parameters.AddWithValue("@ot", c.OwnerTel);
                cmd.Parameters.AddWithValue("@obl", c.OwnerBirthLoc);
                cmd.Parameters.AddWithValue("@dbid", c.ApiDbId ?? "");
                cmd.Parameters.AddWithValue("@arch", c.Archived ? 1 : 0);
                var caseId = Convert.ToInt64(cmd.ExecuteScalar() ?? 0);
        
                if (c.Specification != null)
                {
                    SaveSpecification(caseId, c.Specification, transaction);
                }
        
                foreach (var e in c.Engineers)
                {
                    SaveEngineer(caseId, e, transaction);
                }
        
                foreach (var f in c.Fees)
                {
                    SaveFee(caseId, f, transaction);
                }
        
                foreach (var r in c.Reports)
                {
                    SaveReport(caseId, r, transaction);
                }
            }
        }

        private void SaveSpecification(long caseId, CaseSpecification s, SqliteTransaction? transaction = null)
        {
            lock (_syncRoot)
            {
                using var cmd = _conn.CreateCommand();
                if (transaction != null) cmd.Transaction = transaction;
                cmd.CommandText = @"INSERT INTO Specifications (CaseId, BuildingGroup, RenovationCode, PlanInstructionNo, PlanInstructionType, PlanInstructionDate, StructureType, BlockTitle, BlockCount, Floors, Units, Issuer, PermitNumber, PermitDate, ReleaseDate, LandArea, ParafArea, Address, PlanZone, UsageType, CapacityArea)
                                   VALUES (@cid, @bg, @rc, @pin, @pit, @pid, @st, @bt, @bc, @fl, @un, @is, @pn, @pd, @rd, @la, @pa, @addr, @pz, @ut, @ca)";
                cmd.Parameters.AddWithValue("@cid", caseId);
                cmd.Parameters.AddWithValue("@bg", s.BuildingGroup);
                cmd.Parameters.AddWithValue("@rc", s.RenovationCode);
                cmd.Parameters.AddWithValue("@pin", s.PlanInstructionNo);
                cmd.Parameters.AddWithValue("@pit", s.PlanInstructionType);
                cmd.Parameters.AddWithValue("@pid", s.PlanInstructionDate);
                cmd.Parameters.AddWithValue("@st", s.StructureType);
                cmd.Parameters.AddWithValue("@bt", s.BlockTitle);
                cmd.Parameters.AddWithValue("@bc", s.BlockCount);
                cmd.Parameters.AddWithValue("@fl", s.Floors);
                cmd.Parameters.AddWithValue("@un", s.Units);
                cmd.Parameters.AddWithValue("@is", s.Issuer);
                cmd.Parameters.AddWithValue("@pn", s.PermitNumber);
                cmd.Parameters.AddWithValue("@pd", s.PermitDate);
                cmd.Parameters.AddWithValue("@rd", s.ReleaseDate);
                cmd.Parameters.AddWithValue("@la", s.LandArea);
                cmd.Parameters.AddWithValue("@pa", s.ParafArea);
                cmd.Parameters.AddWithValue("@addr", s.Address);
                cmd.Parameters.AddWithValue("@pz", s.PlanZone);
                cmd.Parameters.AddWithValue("@ut", s.UsageType);
                cmd.Parameters.AddWithValue("@ca", s.CapacityArea);
                cmd.ExecuteNonQuery();
            }
        }

        private void SaveEngineer(long caseId, Engineer e, SqliteTransaction? transaction = null)
        {
            lock (_syncRoot)
            {
                using var cmd = _conn.CreateCommand();
                if (transaction != null) cmd.Transaction = transaction;
                cmd.CommandText = @"INSERT INTO Engineers (CaseId, Discipline, Name, Role) VALUES (@cid, @d, @n, @r)";
                cmd.Parameters.AddWithValue("@cid", caseId);
                cmd.Parameters.AddWithValue("@d", e.Discipline);
                cmd.Parameters.AddWithValue("@n", e.Name);
                cmd.Parameters.AddWithValue("@r", e.Role);
                cmd.ExecuteNonQuery();
            }
        }

        private void SaveFee(long caseId, Fee f, SqliteTransaction? transaction = null)
        {
            lock (_syncRoot)
            {
                using var cmd = _conn.CreateCommand();
                if (transaction != null) cmd.Transaction = transaction;
                cmd.CommandText = @"INSERT INTO Fees (CaseId, Discipline, ServiceType, Stage, StartDate, EndDate, Amount, PayStatus, ConfirmStatus, AmountType, Description)
                                   VALUES (@cid, @d, @st, @s, @sd, @ed, @a, @ps, @cs, @at, @desc)";
                cmd.Parameters.AddWithValue("@cid", caseId);
                cmd.Parameters.AddWithValue("@d", f.Discipline);
                cmd.Parameters.AddWithValue("@st", f.ServiceType);
                cmd.Parameters.AddWithValue("@s", f.Stage);
                cmd.Parameters.AddWithValue("@sd", f.StartDate);
                cmd.Parameters.AddWithValue("@ed", f.EndDate);
                cmd.Parameters.AddWithValue("@a", f.Amount);
                cmd.Parameters.AddWithValue("@ps", f.PayStatus);
                cmd.Parameters.AddWithValue("@cs", f.ConfirmStatus);
                cmd.Parameters.AddWithValue("@at", f.AmountType);
                cmd.Parameters.AddWithValue("@desc", f.Description);
                cmd.ExecuteNonQuery();
            }
        }

        private void SaveReport(long caseId, ReportRecord r, SqliteTransaction? transaction = null)
        {
            lock (_syncRoot)
            {
                using var cmd = _conn.CreateCommand();
                if (transaction != null) cmd.Transaction = transaction;
                cmd.CommandText = @"INSERT INTO Reports (CaseId, RowNo, ReportType, Stage, Engineer, Discipline, VisitDate, CeilingCount, Indicator, HasFile, ImageName)
                                   VALUES (@cid, @rn, @rt, @s, @e, @d, @vd, @cc, @ind, @hf, @img)";
                cmd.Parameters.AddWithValue("@cid", caseId);
                cmd.Parameters.AddWithValue("@rn", r.RowNo);
                cmd.Parameters.AddWithValue("@rt", r.ReportType);
                cmd.Parameters.AddWithValue("@s", r.Stage);
                cmd.Parameters.AddWithValue("@e", r.Engineer);
                cmd.Parameters.AddWithValue("@d", r.Discipline);
                cmd.Parameters.AddWithValue("@vd", r.VisitDate);
                cmd.Parameters.AddWithValue("@cc", r.CeilingCount);
                cmd.Parameters.AddWithValue("@ind", r.Indicator);
                cmd.Parameters.AddWithValue("@hf", r.HasFile ? 1 : 0);
                cmd.Parameters.AddWithValue("@img", r.ImageName ?? "");
                cmd.ExecuteNonQuery();
            }
        }

            // ========== EngineerRegistry Methods ==========

            /// <summary>
            /// پاک کردن جدول EngineerRegistry
            /// </summary>
            public void ClearEngineerRegistry()
            {
                Execute("DELETE FROM EngineerRegistry");
            }

            /// <summary>
            /// درج دسته‌ای رکوردها در EngineerRegistry (با transaction - سریع)
            /// </summary>
            public void BulkInsertEngineerRegistry(List<(string FirstName, string LastName, string FullName, string Phone, string Discipline, string DesignLevel, string SupervisionLevel, string ExecutionLevel)> records)
            {
                lock (_syncRoot)
                {
                    using var transaction = _conn.BeginTransaction();
                    try
                    {
                        using var cmd = _conn.CreateCommand();
                        cmd.Transaction = transaction;
                        cmd.CommandText = @"INSERT INTO EngineerRegistry (FirstName, LastName, FullName, Phone, Discipline, DesignLevel, SupervisionLevel, ExecutionLevel)
                            VALUES (@fn, @ln, @full, @phone, @disc, @dl, @sl, @el)";

                        var pFn = cmd.Parameters.Add("@fn", Microsoft.Data.Sqlite.SqliteType.Text);
                        var pLn = cmd.Parameters.Add("@ln", Microsoft.Data.Sqlite.SqliteType.Text);
                        var pFull = cmd.Parameters.Add("@full", Microsoft.Data.Sqlite.SqliteType.Text);
                        var pPhone = cmd.Parameters.Add("@phone", Microsoft.Data.Sqlite.SqliteType.Text);
                        var pDisc = cmd.Parameters.Add("@disc", Microsoft.Data.Sqlite.SqliteType.Text);
                        var pDl = cmd.Parameters.Add("@dl", Microsoft.Data.Sqlite.SqliteType.Text);
                        var pSl = cmd.Parameters.Add("@sl", Microsoft.Data.Sqlite.SqliteType.Text);
                        var pEl = cmd.Parameters.Add("@el", Microsoft.Data.Sqlite.SqliteType.Text);

                        foreach (var r in records)
                        {
                            pFn.Value = r.FirstName;
                            pLn.Value = r.LastName;
                            pFull.Value = r.FullName;
                            pPhone.Value = r.Phone;
                            pDisc.Value = r.Discipline;
                            pDl.Value = r.DesignLevel;
                            pSl.Value = r.SupervisionLevel;
                            pEl.Value = r.ExecutionLevel;
                            cmd.ExecuteNonQuery();
                        }

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
            /// تطبیق مهندسین جدول Engineers با EngineerRegistry و پر کردن فیلدهای جدید
            /// با transaction و query بهینه
            /// </summary>
            public int MatchEngineersFromRegistry()
            {
                lock (_syncRoot)
                {
                    int matched = 0;

                    // ساخت دیکشنری از EngineerRegistry برای تطبیق سریع
                    var registry = new Dictionary<string, (string Phone, string DL, string SL, string EL)>();
                    using (var cmd = _conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT FullName, Phone, DesignLevel, SupervisionLevel, ExecutionLevel FROM EngineerRegistry";
                        using var r = cmd.ExecuteReader();
                        while (r.Read())
                        {
                            var name = r.GetString(0).Replace(" ", "").Replace("\u200c", "");
                            if (!registry.ContainsKey(name))
                                registry[name] = (r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4));
                        }
                    }

                    // دریافت و آپدیت مهندسین با transaction
                    using var transaction = _conn.BeginTransaction();
                    try
                    {
                        using var selectCmd = _conn.CreateCommand();
                        selectCmd.Transaction = transaction;
                        selectCmd.CommandText = "SELECT Id, Name FROM Engineers";

                        using var updateCmd = _conn.CreateCommand();
                        updateCmd.Transaction = transaction;
                        updateCmd.CommandText = @"UPDATE Engineers SET Phone=@phone, DesignLevel=@dl, SupervisionLevel=@sl, ExecutionLevel=@el WHERE Id=@id";
                        var pPhone = updateCmd.Parameters.Add("@phone", Microsoft.Data.Sqlite.SqliteType.Text);
                        var pDl = updateCmd.Parameters.Add("@dl", Microsoft.Data.Sqlite.SqliteType.Text);
                        var pSl = updateCmd.Parameters.Add("@sl", Microsoft.Data.Sqlite.SqliteType.Text);
                        var pEl = updateCmd.Parameters.Add("@el", Microsoft.Data.Sqlite.SqliteType.Text);
                        var pId = updateCmd.Parameters.Add("@id", Microsoft.Data.Sqlite.SqliteType.Integer);

                        using var r = selectCmd.ExecuteReader();
                        while (r.Read())
                        {
                            var engId = r.GetInt64(0);
                            var engName = r.GetString(1).Replace(" ", "").Replace("\u200c", "");

                            if (registry.TryGetValue(engName, out var data))
                            {
                                pPhone.Value = data.Phone;
                                pDl.Value = data.DL;
                                pSl.Value = data.SL;
                                pEl.Value = data.EL;
                                pId.Value = engId;
                                updateCmd.ExecuteNonQuery();
                                matched++;
                            }
                        }

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }

                    return matched;
                }
            }

            /// <summary>
            /// دریافت تعداد رکوردهای EngineerRegistry
            /// </summary>
            public int GetEngineerRegistryCount()
            {
            lock (_syncRoot)
            {
                using var cmd = _conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM EngineerRegistry";
                return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
            }
            }

            /// <summary>
            /// دریافت تعداد مهندسین آپدیت شده (با شماره تلفن)
            /// </summary>
            public int GetMatchedEngineerCount()
            {
            lock (_syncRoot)
            {
                using var cmd = _conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM Engineers WHERE Phone != '' AND Phone IS NOT NULL";
                return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
            }
            }

            // ═══════════ FeeReceipts (دریافتی) ═══════════
            public List<FeeReceipt> GetFeeReceipts()
            {
                lock (_syncRoot)
                {
                    var list = new List<FeeReceipt>();
                    using var cmd = _conn.CreateCommand();
                    cmd.CommandText = "SELECT Id, ReceiptDate, Amount, RelatedEndDatesRaw, COALESCE(IsAdvance,0), FeeSum, Changes, IsRecentlyUpdated, CreatedAt, UpdatedAt FROM FeeReceipts ORDER BY ReceiptDate DESC, Id DESC";
                    using var r = cmd.ExecuteReader();
                    while (r.Read())
                    {
                        list.Add(new FeeReceipt
                        {
                            Id = r.GetInt64(0),
                            ReceiptDate = r.GetString(1),
                            Amount = r.IsDBNull(2) ? 0 : r.GetInt64(2),
                            RelatedEndDatesRaw = r.GetString(3),
                            IsAdvance = r.GetInt32(4) == 1,
                            FeeSum = r.IsDBNull(5) ? 0 : r.GetInt64(5),
                            Changes = r.GetString(6),
                            IsRecentlyUpdated = r.GetInt32(7) == 1,
                            CreatedAt = r.GetString(8),
                            UpdatedAt = r.GetString(9)
                        });
                    }
                    return list;
                }
            }

            public long InsertFeeReceipt(FeeReceipt rc)
            {
                lock (_syncRoot)
                {
                    using var cmd = _conn.CreateCommand();
                    cmd.CommandText = "INSERT INTO FeeReceipts (ReceiptDate, Amount, RelatedEndDatesRaw, IsAdvance, FeeSum, Changes, IsRecentlyUpdated, CreatedAt, UpdatedAt) VALUES (@d,@a,@r,@adv,@f,@ch,@u,@ca,@ua); SELECT last_insert_rowid();";
                    cmd.Parameters.AddWithValue("@d", rc.ReceiptDate);
                    cmd.Parameters.AddWithValue("@a", rc.Amount);
                    cmd.Parameters.AddWithValue("@r", rc.RelatedEndDatesRaw);
                    cmd.Parameters.AddWithValue("@adv", rc.IsAdvance ? 1 : 0);
                    cmd.Parameters.AddWithValue("@f", rc.FeeSum);
                    cmd.Parameters.AddWithValue("@ch", rc.Changes ?? "");
                    cmd.Parameters.AddWithValue("@u", rc.IsRecentlyUpdated ? 1 : 0);
                    var now = DateTime.Now.ToString("yyyy/MM/dd HH:mm");
                    cmd.Parameters.AddWithValue("@ca", now);
                    cmd.Parameters.AddWithValue("@ua", now);
                    return Convert.ToInt64(cmd.ExecuteScalar() ?? 0);
                }
            }

            public void UpdateFeeReceipt(FeeReceipt rc)
            {
                lock (_syncRoot)
                {
                    using var cmd = _conn.CreateCommand();
                    cmd.CommandText = "UPDATE FeeReceipts SET ReceiptDate=@d, Amount=@a, RelatedEndDatesRaw=@r, IsAdvance=@adv, FeeSum=@f, Changes=@ch, IsRecentlyUpdated=@u, UpdatedAt=@ua WHERE Id=@id";
                    cmd.Parameters.AddWithValue("@d", rc.ReceiptDate);
                    cmd.Parameters.AddWithValue("@a", rc.Amount);
                    cmd.Parameters.AddWithValue("@r", rc.RelatedEndDatesRaw);
                    cmd.Parameters.AddWithValue("@adv", rc.IsAdvance ? 1 : 0);
                    cmd.Parameters.AddWithValue("@f", rc.FeeSum);
                    cmd.Parameters.AddWithValue("@ch", rc.Changes ?? "");
                    cmd.Parameters.AddWithValue("@u", rc.IsRecentlyUpdated ? 1 : 0);
                    cmd.Parameters.AddWithValue("@ua", DateTime.Now.ToString("yyyy/MM/dd HH:mm"));
                    cmd.Parameters.AddWithValue("@id", rc.Id);
                    cmd.ExecuteNonQuery();
                }
            }

            public void DeleteFeeReceipt(long id)
            {
                lock (_syncRoot)
                {
                    using var cmd = _conn.CreateCommand();
                    cmd.CommandText = "DELETE FROM FeeReceipts WHERE Id=@id";
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }

            public void ClearRecentFlags()
            {
                Execute("UPDATE FeeReceipts SET IsRecentlyUpdated=0, Changes='' WHERE IsRecentlyUpdated=1");
            }

            /// <summary>جمع حق‌الزحمه بر اساس StartDate فقط برای PayStatus=پرداخت شده در ActiveSnapshot</summary>
            public Dictionary<string, long> GetFeeSumByStartDate()
            {
                return GetFeeSumByStartDatePaid();
            }

            public Dictionary<string, long> GetFeeSumByStartDatePaid()
            {
                lock (_syncRoot)
                {
                    var dict = new Dictionary<string, long>();
                    var sid = GetActiveSnapshotId();
                    if (sid == 0) return dict;
                    using var cmd = _conn.CreateCommand();
                    cmd.CommandText = "SELECT Fees.StartDate, Fees.Amount FROM Fees JOIN Cases ON Fees.CaseId=Cases.Id WHERE Cases.SnapshotId=@sid AND Fees.PayStatus='پرداخت شده' AND Fees.StartDate IS NOT NULL AND Fees.StartDate<>''";
                    cmd.Parameters.AddWithValue("@sid", sid);
                    using var r = cmd.ExecuteReader();
                    while (r.Read())
                    {
                        var s = r.GetString(0)?.Trim() ?? "";
                        var amtRaw = r.IsDBNull(1) ? "" : r.GetString(1);
                        if (string.IsNullOrWhiteSpace(s)) continue;
                        long v = ParseAmountStatic(amtRaw);
                        if (!dict.ContainsKey(s)) dict[s] = 0;
                        dict[s] += v;
                    }
                    return dict;
                }
            }

            /// <summary>جمع حق‌الزحمه بر اساس StartDate فقط برای ConfirmStatus=تایید شده و StartDate دار</summary>
            public Dictionary<string, long> GetFeeSumByStartDateConfirmed()
            {
                lock (_syncRoot)
                {
                    var dict = new Dictionary<string, long>();
                    var sid = GetActiveSnapshotId();
                    if (sid == 0) return dict;
                    using var cmd = _conn.CreateCommand();
                    cmd.CommandText = "SELECT Fees.StartDate, Fees.Amount FROM Fees JOIN Cases ON Fees.CaseId=Cases.Id WHERE Cases.SnapshotId=@sid AND Fees.PayStatus='تایید شده' AND Fees.StartDate IS NOT NULL AND Fees.StartDate<>''";
                    cmd.Parameters.AddWithValue("@sid", sid);
                    using var r = cmd.ExecuteReader();
                    while (r.Read())
                    {
                        var s = r.GetString(0)?.Trim() ?? "";
                        var amtRaw = r.IsDBNull(1) ? "" : r.GetString(1);
                        if (string.IsNullOrWhiteSpace(s)) continue;
                        long v = ParseAmountStatic(amtRaw);
                        if (!dict.ContainsKey(s)) dict[s] = 0;
                        dict[s] += v;
                    }
                    return dict;
                }
            }

            public Dictionary<string, long> GetFeeSumByStartDateForReceipt(bool isAdvance) => isAdvance ? GetFeeSumByStartDateConfirmed() : GetFeeSumByStartDatePaid();

            // سازگاری قدیمی — حالا روی StartDate+پرداخت شده
            public Dictionary<string, long> GetFeeSumByEndDate() => GetFeeSumByStartDate();

            private static long ParseAmountStatic(string amount)
            {
                if (string.IsNullOrWhiteSpace(amount)) return 0;
                var c = amount.Replace("ریال", "").Replace(",", "").Replace("٬", "").Replace("،", "").Replace(" ", "").Trim();
                c = c.Replace("۰", "0").Replace("۱", "1").Replace("۲", "2").Replace("۳", "3").Replace("۴", "4").Replace("۵", "5").Replace("۶", "6").Replace("۷", "7").Replace("۸", "8").Replace("۹", "9")
                     .Replace("٠", "0").Replace("١", "1").Replace("٢", "2").Replace("٣", "3").Replace("٤", "4").Replace("٥", "5").Replace("٦", "6").Replace("٧", "7").Replace("٨", "8").Replace("٩", "9");
                return long.TryParse(c, out var x) ? x : 0;
            }

            public void BulkInsertFeeReceipts(List<FeeReceipt> list)
            {
                lock (_syncRoot)
                {
                    using var tx = _conn.BeginTransaction();
                    try
                    {
                        foreach (var rc in list) InsertFeeReceipt(rc);
                        tx.Commit();
                    }
                    catch { tx.Rollback(); throw; }
                }
            }
}
