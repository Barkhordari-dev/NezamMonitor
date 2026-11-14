using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using NezamMonitorPro.Core.Models;

namespace NezamMonitorPro.Core.Data;
/// <summary>SQLite persistence for cases, snapshots, and changes.</summary>
public sealed partial class NezamDatabase : IDisposable
{
    private readonly SqliteConnection _conn;

    // یک اتصال مشترک بین نخ UI و Task.Runها دست‌به‌دست می‌شود؛ همه
    // دسترسی‌ها باید زیر همین قفل سریال شوند (lock در C# روی یک نخ
    // re-entrant است، پس فراخوانی متدها از داخل هم مشکلی ندارد)
    private readonly object _syncRoot = new();

    /// <summary>قفل اتصال مشترک؛ کد بیرون از این کلاس اگر مستقیم با
    /// اتصال کار می‌کند باید زیر همین قفل باشد.</summary>
    public object SyncRoot => _syncRoot;

    /// <summary>اتصال باز پایگاه‌داده (برای ایمپورت/مهاجرت داده استفاده می‌شود).</summary>
    public SqliteConnection Connection => _conn;

    public NezamDatabase(SqliteConnection connection)
    {
        lock (_syncRoot)
        {
            _conn = connection ?? throw new ArgumentNullException(nameof(connection));
            if (_conn.State != System.Data.ConnectionState.Open)
                _conn.Open();
            Initialize();
        }
    }

    public NezamDatabase(string dbPath)
    {
        lock (_syncRoot)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            _conn = new SqliteConnection($"Data Source={dbPath}");
            _conn.Open();
            Initialize();
        }
    }

    public void Dispose() { lock (_syncRoot) { _conn.Dispose(); } }

    private void Execute(string sql, SqliteTransaction? transaction = null)
    {
        lock (_syncRoot)
        {
            using var cmd = _conn.CreateCommand();
            if (transaction != null) cmd.Transaction = transaction;
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
    }

    private static string SafeGetString(SqliteDataReader reader, string column)
    {
        try
        {
            var idx = reader.GetOrdinal(column);
            return reader.IsDBNull(idx) ? "" : reader.GetString(idx);
        }
        catch { return ""; }
    }

    private static int SafeGetInt(SqliteDataReader reader, string column)
    {
        try
        {
            var idx = reader.GetOrdinal(column);
            return reader.IsDBNull(idx) ? 0 : reader.GetInt32(idx);
        }
        catch { return 0; }
    }

    private void Initialize()
    {
        // کلیدهای خارجی را از همان اول روشن کن تا حذف والد دارای فرزند
        // خطا بدهد و داده یتیم به‌جا نماند (پیش‌فرض SQLite خاموش است)
        Execute("PRAGMA foreign_keys = ON;");

        Execute("""
            CREATE TABLE IF NOT EXISTS Snapshots (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CreatedAt TEXT NOT NULL,
                CaseCount INTEGER DEFAULT 0,
                Status TEXT DEFAULT 'pending'
            )
            """);

        Execute("""
            CREATE TABLE IF NOT EXISTS Cases (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SnapshotId INTEGER NOT NULL,
                CaseNumber TEXT NOT NULL,
                Serial TEXT DEFAULT '',
                Owner TEXT DEFAULT '',
                OwnerMobile TEXT DEFAULT '',
                Responsibility TEXT DEFAULT '',
                CapacityDate TEXT DEFAULT '',
                Office TEXT DEFAULT '',
                ReportDate1 TEXT DEFAULT '',
                ReportDate2 TEXT DEFAULT '',
                ReportDate3 TEXT DEFAULT '',
                OwnerFather TEXT DEFAULT '',
                OwnerNationalCode TEXT DEFAULT '',
                OwnerAddress TEXT DEFAULT '',
                OwnerZip TEXT DEFAULT '',
                OwnerTel TEXT DEFAULT '',
                OwnerBirthLoc TEXT DEFAULT '',
                ApiDbId TEXT DEFAULT '',
                Archived INTEGER DEFAULT 0
            )
            """);
        // Migration: ستون‌های مالک برای DBهای قدیمی (اگر پوشه data پاک و دوباره ساخته شد هم پوشش می‌دهد - CREATE بالا شامل آنهاست)
        try { Execute("ALTER TABLE Cases ADD COLUMN OwnerFather TEXT DEFAULT ''"); } catch { }
        try { Execute("ALTER TABLE Cases ADD COLUMN OwnerNationalCode TEXT DEFAULT ''"); } catch { }
        try { Execute("ALTER TABLE Cases ADD COLUMN OwnerAddress TEXT DEFAULT ''"); } catch { }
        try { Execute("ALTER TABLE Cases ADD COLUMN OwnerZip TEXT DEFAULT ''"); } catch { }
        try { Execute("ALTER TABLE Cases ADD COLUMN OwnerTel TEXT DEFAULT ''"); } catch { }
        try { Execute("ALTER TABLE Cases ADD COLUMN OwnerBirthLoc TEXT DEFAULT ''"); } catch { }
        try { Execute("ALTER TABLE Cases ADD COLUMN ApiDbId TEXT DEFAULT ''"); } catch { }
        try { Execute("ALTER TABLE Cases ADD COLUMN Archived INTEGER DEFAULT 0"); } catch { }

        Execute("""
            CREATE TABLE IF NOT EXISTS Specifications (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CaseId INTEGER NOT NULL,
                BuildingGroup TEXT DEFAULT '',
                RenovationCode TEXT DEFAULT '',
                PlanInstructionNo TEXT DEFAULT '',
                PlanInstructionType TEXT DEFAULT '',
                PlanInstructionDate TEXT DEFAULT '',
                StructureType TEXT DEFAULT '',
                BlockTitle TEXT DEFAULT '',
                BlockCount TEXT DEFAULT '',
                Floors TEXT DEFAULT '',
                Units TEXT DEFAULT '',
                Issuer TEXT DEFAULT '',
                PermitNumber TEXT DEFAULT '',
                PermitDate TEXT DEFAULT '',
                ReleaseDate TEXT DEFAULT '',
                LandArea TEXT DEFAULT '',
                ParafArea TEXT DEFAULT '',
                Address TEXT DEFAULT '',
                PlanZone TEXT DEFAULT '',
                UsageType TEXT DEFAULT '',
                CapacityArea TEXT DEFAULT '',
                FOREIGN KEY (CaseId) REFERENCES Cases(Id)
            )
            """);

        Execute("""
            CREATE TABLE IF NOT EXISTS Engineers (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CaseId INTEGER NOT NULL,
                Discipline TEXT DEFAULT '',
                Name TEXT DEFAULT '',
                Role TEXT DEFAULT '',
                FOREIGN KEY (CaseId) REFERENCES Cases(Id)
            )
            """);

        Execute("""
            CREATE TABLE IF NOT EXISTS Fees (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CaseId INTEGER NOT NULL,
                Discipline TEXT DEFAULT '',
                ServiceType TEXT DEFAULT '',
                Stage TEXT DEFAULT '',
                StartDate TEXT DEFAULT '',
                EndDate TEXT DEFAULT '',
                Amount TEXT DEFAULT '',
                PayStatus TEXT DEFAULT '',
                ConfirmStatus TEXT DEFAULT '',
                AmountType TEXT DEFAULT '',
                Description TEXT DEFAULT '',
                FOREIGN KEY (CaseId) REFERENCES Cases(Id)
            )
            """);

        Execute("""
            CREATE TABLE IF NOT EXISTS Reports (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CaseId INTEGER NOT NULL,
                RowNo TEXT DEFAULT '',
                ReportType TEXT DEFAULT '',
                Stage TEXT DEFAULT '',
                Engineer TEXT DEFAULT '',
                Discipline TEXT DEFAULT '',
                VisitDate TEXT DEFAULT '',
                CeilingCount TEXT DEFAULT '',
                Indicator TEXT DEFAULT '',
                HasFile INTEGER DEFAULT 0,
                ImageName TEXT DEFAULT '',
                FOREIGN KEY (CaseId) REFERENCES Cases(Id)
            )
            """);

        // Migration: add columns if missing
        try { Execute("ALTER TABLE Reports ADD COLUMN ImageName TEXT DEFAULT ''"); } catch { }

        Execute("""
            CREATE TABLE IF NOT EXISTS Changes (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SnapshotId INTEGER NOT NULL,
                CaseNumber TEXT NOT NULL,
                Owner TEXT DEFAULT '',
                ChangeType TEXT DEFAULT '',
                FieldName TEXT DEFAULT '',
                OldValue TEXT DEFAULT '',
                NewValue TEXT DEFAULT '',
                FOREIGN KEY (SnapshotId) REFERENCES Snapshots(Id)
            )
            """);

        Execute("""
            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT DEFAULT ''
            )
            """);

        Execute("""
            CREATE TABLE IF NOT EXISTS ActiveSnapshot (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                SnapshotId INTEGER NOT NULL
            )
            """);

        Execute("""
            CREATE TABLE IF NOT EXISTS GeneratedReports (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CaseNumber TEXT NOT NULL,
                Stage INTEGER NOT NULL,
                TemplateName TEXT DEFAULT '',
                OutputPath TEXT DEFAULT '',
                CreatedAt TEXT DEFAULT '',
                Status TEXT DEFAULT 'generated',
                SentAt TEXT DEFAULT '',
                OwnerName TEXT DEFAULT '',
                NumberFormat TEXT DEFAULT 'Persian',
                PdfPath TEXT DEFAULT '',
                FileType TEXT DEFAULT 'word'
            )
            """);

        // Migration: add columns if missing
        try { Execute("ALTER TABLE GeneratedReports ADD COLUMN PdfPath TEXT DEFAULT ''"); } catch { }
        try { Execute("ALTER TABLE GeneratedReports ADD COLUMN FileType TEXT DEFAULT 'word'"); } catch { }
        try { Execute("ALTER TABLE GeneratedReports ADD COLUMN OwnerName TEXT DEFAULT ''"); } catch { }
        try { Execute("ALTER TABLE GeneratedReports ADD COLUMN NumberFormat TEXT DEFAULT 'Persian'"); } catch { }
        try { Execute("ALTER TABLE GeneratedReports ADD COLUMN FileExists INTEGER DEFAULT 1"); } catch { }
        try { Execute("ALTER TABLE GeneratedReports ADD COLUMN FolderName TEXT"); } catch { }
        try { Execute("ALTER TABLE GeneratedReports ADD COLUMN ActionLog TEXT"); } catch { }
        try { Execute("ALTER TABLE GeneratedReports ADD COLUMN FileHash TEXT DEFAULT ''"); } catch { }
        try { Execute("ALTER TABLE LastScanResults ADD COLUMN PermitDate TEXT DEFAULT ''"); } catch { }
        try { Execute("ALTER TABLE LastScanResults ADD COLUMN GeneratedAtShamsi TEXT DEFAULT ''"); } catch { }
        
        // FollowUpEdits table for persisting follow-up edits
        Execute("""
            CREATE TABLE IF NOT EXISTS FollowUpEdits (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CaseNumber TEXT NOT NULL,
                Description TEXT DEFAULT '',
                CaseNumberEdit TEXT DEFAULT '',
                OwnerEdit TEXT DEFAULT '',
                AddressEdit TEXT DEFAULT '',
                OwnerMobileEdit TEXT DEFAULT '',
                CreatedAt TEXT DEFAULT '',
                UpdatedAt TEXT DEFAULT '',
                UNIQUE(CaseNumber)
            )
            """);

        // EngineerRegistry: اطلاعات تکمیلی مهندسین از فایل اکسل
        Execute("""
            CREATE TABLE IF NOT EXISTS EngineerRegistry (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FirstName TEXT DEFAULT '',
                LastName TEXT DEFAULT '',
                FullName TEXT DEFAULT '',
                Phone TEXT DEFAULT '',
                Discipline TEXT DEFAULT '',
                DesignLevel TEXT DEFAULT '',
                SupervisionLevel TEXT DEFAULT '',
                ExecutionLevel TEXT DEFAULT ''
            )
            """);

        // Migration: اضافه کردن فیلدهای جدید به جدول Engineers
        try { Execute("ALTER TABLE Engineers ADD COLUMN Phone TEXT DEFAULT ''"); } catch { }
        try { Execute("ALTER TABLE Engineers ADD COLUMN DesignLevel TEXT DEFAULT ''"); } catch { }
        try { Execute("ALTER TABLE Engineers ADD COLUMN SupervisionLevel TEXT DEFAULT ''"); } catch { }
        try { Execute("ALTER TABLE Engineers ADD COLUMN ExecutionLevel TEXT DEFAULT ''"); } catch { }

        // ScanCache: کش وضعیت اسکن فایل‌ها برای مقایسه بین اسکن‌ها
        Execute("""
            CREATE TABLE IF NOT EXISTS ScanCache (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ReportKey TEXT NOT NULL,
                HasFile INTEGER NOT NULL,
                ScanDate TEXT NOT NULL,
                UNIQUE(ReportKey)
            )
            """);

        // FeeReceipts: دریافتی‌ها — هر ردیف یک واریز با چند تاریخ پایان مرتبط
        Execute("""
            CREATE TABLE IF NOT EXISTS FeeReceipts (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ReceiptDate TEXT DEFAULT '',
                Amount INTEGER DEFAULT 0,
                RelatedEndDatesRaw TEXT DEFAULT '',
                IsAdvance INTEGER DEFAULT 0,
                FeeSum INTEGER DEFAULT 0,
                Changes TEXT DEFAULT '',
                IsRecentlyUpdated INTEGER DEFAULT 0,
                CreatedAt TEXT DEFAULT '',
                UpdatedAt TEXT DEFAULT ''
            )
            """);
        try { Execute("ALTER TABLE FeeReceipts ADD COLUMN IsAdvance INTEGER DEFAULT 0"); } catch { }

        // LastScanResults: ذخیره نتیجه آخرین اسکن (فقط آخرین اسکن)
        Execute("""
            CREATE TABLE IF NOT EXISTS LastScanResults (
                CaseNumber TEXT NOT NULL,
                Stage INTEGER NOT NULL,
                Owner TEXT DEFAULT '',
                FullAddress TEXT DEFAULT '',
                PermitNumber TEXT DEFAULT '',
                Companion TEXT DEFAULT '',
                StageName TEXT DEFAULT '',
                Status TEXT DEFAULT '',
                GeneratedAt TEXT DEFAULT '',
                Deadline TEXT DEFAULT '',
                DaysRemaining TEXT DEFAULT '',
                DeadlineStatus TEXT DEFAULT '',
                BuildingGroup TEXT DEFAULT '',
                HasReport INTEGER DEFAULT 0,
                PermitDate TEXT DEFAULT '',
                GeneratedAtShamsi TEXT DEFAULT '',
                ScanDate TEXT DEFAULT '',
                UNIQUE(CaseNumber, Stage)
            )
            """);

        // Indexes for the N+1-free LoadCases below: without them every child
        // lookup (and the Cases-by-snapshot scan) is a full table scan, so
        // loading grows quadratically with the data. CREATE INDEX IF NOT
        // EXISTS is a no-op once the index exists.
        Execute("CREATE INDEX IF NOT EXISTS idx_cases_snapshot ON Cases (SnapshotId)");
        Execute("CREATE INDEX IF NOT EXISTS idx_spec_caseid ON Specifications (CaseId)");
        Execute("CREATE INDEX IF NOT EXISTS idx_eng_caseid ON Engineers (CaseId)");
        Execute("CREATE INDEX IF NOT EXISTS idx_fees_caseid ON Fees (CaseId)");
        Execute("CREATE INDEX IF NOT EXISTS idx_reports_caseid ON Reports (CaseId)");

        // Hidden menu overrides (v11, mirrors Android's hidden_overrides):
        // "after every sync/import, set this field of this record to this
        // value". Temporary rows are deleted after the next sync/import.
        Execute("""
            CREATE TABLE IF NOT EXISTS HiddenOverrides (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RecordType TEXT NOT NULL,
                RecordKey TEXT NOT NULL,
                Field TEXT NOT NULL,
                Value TEXT DEFAULT '',
                Mode TEXT DEFAULT 'perm',
                UpdatedAt TEXT DEFAULT ''
            )
            """);
        Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_hidden_overrides_key ON HiddenOverrides (RecordType, RecordKey, Field)");
    }

    // ── Hidden menu: database tools ────────────────────────────────────

    /// <summary>Runs PRAGMA integrity_check; returns "ok" when healthy,
    /// otherwise the first problem lines.</summary>
    public string CheckIntegrity()
    {
        lock (_syncRoot)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check";
            using var r = cmd.ExecuteReader();
            var problems = new List<string>();
            while (r.Read())
            {
                var line = r.GetString(0);
                if (!string.Equals(line, "ok", StringComparison.OrdinalIgnoreCase))
                    problems.Add(line);
            }
            return problems.Count == 0 ? "ok" : string.Join("\n", problems.Take(5));
        }
    }

    public void Vacuum()
    {
        lock (_syncRoot)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "VACUUM";
            cmd.ExecuteNonQuery();
        }
    }

    public Dictionary<string, long> GetRowCounts()
    {
        lock (_syncRoot)
        {
            var counts = new Dictionary<string, long>();
            foreach (var table in new[] { "Cases", "Fees", "Engineers", "Reports", "FeeReceipts", "GeneratedReports", "Snapshots" })
            {
                using var cmd = _conn.CreateCommand();
                cmd.CommandText = $"SELECT COUNT(*) FROM {table}";
                counts[table] = Convert.ToInt64(cmd.ExecuteScalar() ?? 0);
            }
            return counts;
        }
    }

    /// <summary>
    /// Wipes all user data from the database using the SAME open connection
    /// as the rest of the app (avoids SQLite file-lock conflicts that made
    /// the previous "factory reset" fail).
    /// Returns the list of tables that were cleared.
    /// </summary>
    public List<string> ClearAllData()
    {
        lock (_syncRoot)
        {
            var cleared = new List<string>();

            // Order matters: children before parents (FK constraints).
            // NOTE: the "Settings" table itself is deliberately NOT cleared — it stores the
            // user's theme/fonts/dimensions, and wiping it would reset the whole
            // UI to defaults on the next start (a silent side-effect of a "data"
            // reset). The theme is reset explicitly by the caller instead.
            // Only the sensitive Settings keys (login/token/hidden menu) are
            // deleted below, in the same transaction.
            string[] tables = {
                "FollowUpEdits", "Changes", "Reports", "Fees", "FeeReceipts",
                "Engineers", "Specifications", "Cases", "GeneratedReports",
                "ScanCache", "LastScanResults", "Snapshots", "ActiveSnapshot",
                "HiddenOverrides"
            };

            using var tx = _conn.BeginTransaction();
            foreach (var table in tables)
            {
                try
                {
                    using var cmd = _conn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = "DELETE FROM \"" + table + "\"";
                    cmd.ExecuteNonQuery();
                    cleared.Add(table);
                }
                catch
                {
                    // Table may not exist in an older schema — skip it
                }
            }

            // کلیدهای حساس Settings (ورود/توکن/منوی مخفی) هم در همین
            // عملیات پاک می‌شوند؛ بقیه تنظیمات (تم، فونت، ابعاد) دست نمی‌خورند
            try
            {
                using var delKeys = _conn.CreateCommand();
                delKeys.Transaction = tx;
                delKeys.CommandText = "DELETE FROM Settings WHERE Key IN ('username','password','api_token','hidden_menu_folder_id','hidden_menu_cached_name')";
                delKeys.ExecuteNonQuery();
                cleared.Add("Settings (sensitive keys)");
            }
            catch
            {
                // Settings may not exist in an older schema — skip it
            }

            tx.Commit();

            // Reclaim the space and rebuild the indexes so the file does not
            // stay bloated after a wipe. VACUUM cannot run inside a transaction,
            // so it is done after the commit.
            try
            {
                using var vac = _conn.CreateCommand();
                vac.CommandText = "VACUUM";
                vac.ExecuteNonQuery();
            }
            catch { /* VACUUM is best-effort; the data is already deleted. */ }

            return cleared;
        }
    }
}
