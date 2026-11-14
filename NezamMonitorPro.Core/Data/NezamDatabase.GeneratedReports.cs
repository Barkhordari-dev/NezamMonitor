using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using NezamMonitorPro.Core.Models;

namespace NezamMonitorPro.Core.Data;

public sealed partial class NezamDatabase
{
    public List<GeneratedReportRow> GetGeneratedReports()
    {
        lock (_syncRoot)
        {
            var list = new List<GeneratedReportRow>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT CaseNumber, Stage, OutputPath, CreatedAt, Status, COALESCE(OwnerName,''), COALESCE(NumberFormat,'Persian'), COALESCE(FolderName,''), COALESCE(FileHash,''), COALESCE(PdfPath,''), COALESCE(FileType,'word') FROM GeneratedReports ORDER BY CaseNumber, Stage";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new GeneratedReportRow(r.GetString(0), r.GetInt32(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetString(6), r.GetString(7), r.GetString(8), r.GetString(9), r.GetString(10)));
            return list;
        }
    }

    public bool HasGeneratedReport(string caseNumber, int stage)
    {
        lock (_syncRoot)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM GeneratedReports WHERE CaseNumber=@cn AND Stage=@st";
            cmd.Parameters.AddWithValue("@cn", caseNumber);
            cmd.Parameters.AddWithValue("@st", stage);
            return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
        }
    }

    // ─── ScanCache: کش وضعیت اسکن ───

    public void SaveScanCache(Dictionary<string, bool> scanState)
    {
        lock (_syncRoot)
        {
            using var tx = _conn.BeginTransaction();
            using (var delCmd = _conn.CreateCommand())
            {
                delCmd.CommandText = "DELETE FROM ScanCache";
                delCmd.ExecuteNonQuery();
            }
            using var insCmd = _conn.CreateCommand();
            insCmd.CommandText = "INSERT INTO ScanCache (ReportKey, HasFile, ScanDate) VALUES (@key, @has, @date)";
            var keyParam = insCmd.Parameters.Add("@key", SqliteType.Text);
            var hasParam = insCmd.Parameters.Add("@has", SqliteType.Integer);
            var dateParam = insCmd.Parameters.Add("@date", SqliteType.Text);
            var now = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            foreach (var kv in scanState)
            {
                keyParam.Value = kv.Key;
                hasParam.Value = kv.Value ? 1 : 0;
                dateParam.Value = now;
                insCmd.ExecuteNonQuery();
            }
            tx.Commit();
        }
    }

    public Dictionary<string, bool> LoadScanCache()
    {
        lock (_syncRoot)
        {
            var cache = new Dictionary<string, bool>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT ReportKey, HasFile FROM ScanCache";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                cache[r.GetString(0)] = r.GetInt32(1) == 1;
            return cache;
        }
    }

    // ─── LastScanResults: ذخیره نتیجه آخرین اسکن ───

    /// <summary>
    /// ذخیره نتایج اسکن. فقط آخرین اسکن نگه داشته می‌شه (DELETE + INSERT).
    /// </summary>
    public void SaveLastScanResults(List<LastScanResultItem> items)
    {
        lock (_syncRoot)
        {
            using var tx = _conn.BeginTransaction();
            using (var delCmd = _conn.CreateCommand())
            {
                delCmd.CommandText = "DELETE FROM LastScanResults";
                delCmd.ExecuteNonQuery();
            }
            using var insCmd = _conn.CreateCommand();
            insCmd.CommandText = @"INSERT INTO LastScanResults 
                (CaseNumber, Stage, Owner, FullAddress, PermitNumber, Companion, 
                 StageName, Status, GeneratedAt, GeneratedAtShamsi, Deadline, DaysRemaining, 
                 DeadlineStatus, BuildingGroup, HasReport, PermitDate, ScanDate) 
                VALUES (@cn, @st, @ow, @fa, @pn, @co, @sn, @sts, @ga, @gsh, @dl, @dr, @ds, @bg, @hr, @pd2, @sd)";
            var cn = insCmd.Parameters.Add("@cn", SqliteType.Text);
            var st = insCmd.Parameters.Add("@st", SqliteType.Integer);
            var ow = insCmd.Parameters.Add("@ow", SqliteType.Text);
            var fa = insCmd.Parameters.Add("@fa", SqliteType.Text);
            var pn = insCmd.Parameters.Add("@pn", SqliteType.Text);
            var co = insCmd.Parameters.Add("@co", SqliteType.Text);
            var sn = insCmd.Parameters.Add("@sn", SqliteType.Text);
            var sts = insCmd.Parameters.Add("@sts", SqliteType.Text);
            var ga = insCmd.Parameters.Add("@ga", SqliteType.Text);
            var gsh = insCmd.Parameters.Add("@gsh", SqliteType.Text);
            var dl = insCmd.Parameters.Add("@dl", SqliteType.Text);
            var dr = insCmd.Parameters.Add("@dr", SqliteType.Text);
            var ds = insCmd.Parameters.Add("@ds", SqliteType.Text);
            var bg = insCmd.Parameters.Add("@bg", SqliteType.Text);
            var hr = insCmd.Parameters.Add("@hr", SqliteType.Integer);
            var pd2 = insCmd.Parameters.Add("@pd2", SqliteType.Text);
            var sd = insCmd.Parameters.Add("@sd", SqliteType.Text);
            var now = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            foreach (var item in items)
            {
                cn.Value = item.CaseNumber;
                st.Value = item.Stage;
                ow.Value = item.Owner;
                fa.Value = item.FullAddress;
                pn.Value = item.PermitNumber;
                co.Value = item.Companion;
                sn.Value = item.StageName;
                sts.Value = item.Status;
                ga.Value = item.GeneratedAt;
                gsh.Value = item.GeneratedAtShamsi;
                dl.Value = item.Deadline;
                dr.Value = item.DaysRemaining;
                ds.Value = item.DeadlineStatus;
                bg.Value = item.BuildingGroup;
                hr.Value = item.HasReport ? 1 : 0;
                pd2.Value = item.PermitDate;
                sd.Value = now;
                insCmd.ExecuteNonQuery();
            }
            tx.Commit();
        }
    }

    /// <summary>
    /// لود نتایج اسکن قبلی. اگه خالی باشه یعنی هنوز اسکنی انجام نشده.
    /// </summary>
    public List<LastScanResultItem> LoadLastScanResults()
    {
        lock (_syncRoot)
        {
            var list = new List<LastScanResultItem>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM LastScanResults";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new LastScanResultItem
                {
                    CaseNumber = r.GetString("CaseNumber"),
                    Stage = r.GetInt32("Stage"),
                    Owner = r.GetString("Owner"),
                    FullAddress = r.GetString("FullAddress"),
                    PermitNumber = r.GetString("PermitNumber"),
                    Companion = r.GetString("Companion"),
                    StageName = r.GetString("StageName"),
                    Status = r.GetString("Status"),
                    GeneratedAt = r.GetString("GeneratedAt"),
                    GeneratedAtShamsi = SafeGetString(r, "GeneratedAtShamsi"),
                    Deadline = r.GetString("Deadline"),
                    DaysRemaining = r.GetString("DaysRemaining"),
                    DeadlineStatus = r.GetString("DeadlineStatus"),
                    BuildingGroup = r.GetString("BuildingGroup"),
                    PermitDate = SafeGetString(r, "PermitDate"),
                    HasReport = r.GetInt32("HasReport") == 1
                });
            }
            return list;
        }
    }

    public void DeleteGeneratedReport(string caseNumber, int stage)
    {
        lock (_syncRoot)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM GeneratedReports WHERE CaseNumber=@cn AND Stage=@st";
            cmd.Parameters.AddWithValue("@cn", caseNumber);
            cmd.Parameters.AddWithValue("@st", stage);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// حذف لاگ‌هایی که فایل گزارششان موجود نیست.
    /// قانون: برای هر CaseNumber+Stage حداقل یک لاگ باقی می‌ماند (آخرین لاگ، حتی اگر فایلش حذف شده).
    /// </summary>
    public int CleanupMissingReports()
    {
        lock (_syncRoot)
        {
            var all = GetGeneratedReports();
            int deleted = 0;

            // گروه‌بندی بر اساس CaseNumber+Stage
            var groups = all
                .GroupBy(r => $"{r.CaseNumber}|{r.Stage}")
                .ToList();

            foreach (var group in groups)
            {
                var items = group.OrderByDescending(r => r.CreatedAt).ToList();
                if (items.Count <= 1) continue; // فقط یک لاگ داره → حفظ میشه

                // آخرین لاگ همیشه حفظ میشه (حتی اگر فایلش حذف شده باشه)
                var keep = items[0]; // آخرین (جدیدترین)

                // بقیه لاگ‌ها: اگر فایلشون موجود نیست → حذف
                foreach (var item in items.Skip(1))
                {
                    if (!File.Exists(item.OutputPath))
                    {
                        using var cmd = _conn.CreateCommand();
                        cmd.CommandText = "DELETE FROM GeneratedReports WHERE CaseNumber=@cn AND Stage=@st AND OutputPath=@op";
                        cmd.Parameters.AddWithValue("@cn", item.CaseNumber);
                        cmd.Parameters.AddWithValue("@st", item.Stage);
                        cmd.Parameters.AddWithValue("@op", item.OutputPath);
                        cmd.ExecuteNonQuery();
                        deleted++;
                    }
                }
            }
            return deleted;
        }
    }

    public void DeleteGeneratedReportByPath(string outputPath)
    {
        lock (_syncRoot)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM GeneratedReports WHERE OutputPath=@op";
            cmd.Parameters.AddWithValue("@op", outputPath);
            cmd.ExecuteNonQuery();
        }
    }

    public static string ComputeFileHash(string filePath)
    {
        if (!File.Exists(filePath)) return "";
        using var sha = System.Security.Cryptography.SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    public void SaveGeneratedReport(string caseNumber, int stage, string template, string outputPath, string ownerName = "", string numberFormat = "Persian", string fileHash = "", string pdfPath = "", string fileType = "word")
    {
        lock (_syncRoot)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO GeneratedReports (CaseNumber, Stage, TemplateName, OutputPath, CreatedAt, Status, OwnerName, NumberFormat, FileHash, PdfPath, FileType)
                VALUES (@cn, @st, @tp, @op, @at, 'generated', @on, @nf, @fh, @pp, @ft)";
            cmd.Parameters.AddWithValue("@cn", caseNumber);
            cmd.Parameters.AddWithValue("@st", stage);
            cmd.Parameters.AddWithValue("@tp", template);
            cmd.Parameters.AddWithValue("@op", outputPath);
            cmd.Parameters.AddWithValue("@at", DateTime.Now.ToString("yyyy/MM/dd HH:mm", System.Globalization.CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("@on", ownerName);
            cmd.Parameters.AddWithValue("@nf", numberFormat);
            cmd.Parameters.AddWithValue("@fh", fileHash);
            cmd.Parameters.AddWithValue("@pp", pdfPath ?? "");
            cmd.Parameters.AddWithValue("@ft", string.IsNullOrEmpty(fileType) ? "word" : fileType);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Records the PDF produced from a generated Word report (P2/V5).
    /// Updates the matching row (same case + stage, newest first).
    /// </summary>
    public void SetGeneratedReportPdf(string caseNumber, int stage, string pdfPath)
    {
        lock (_syncRoot)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = @"UPDATE GeneratedReports SET PdfPath=@pp, FileType='pdf'
                WHERE Id=(SELECT Id FROM GeneratedReports WHERE CaseNumber=@cn AND Stage=@st ORDER BY rowid DESC LIMIT 1)";
            cmd.Parameters.AddWithValue("@pp", pdfPath ?? "");
            cmd.Parameters.AddWithValue("@cn", caseNumber);
            cmd.Parameters.AddWithValue("@st", stage);
            cmd.ExecuteNonQuery();
        }
    }

    public (int added, int removed, int existing) SyncOutputFolder(string outputPath)
    {
        lock (_syncRoot)
        {
            int added = 0, removed = 0, existing = 0;

            // Get all existing records
            var existingReports = GetGeneratedReports();
            var existingPaths = new HashSet<string>(existingReports.Select(r => r.OutputPath));

            // Scan output folder
            var allFiles = new HashSet<string>();
            if (Directory.Exists(outputPath))
            {
                foreach (var file in Directory.GetFiles(outputPath, "*.docx", SearchOption.AllDirectories))
                {
                    allFiles.Add(file);
                }
            }

            // Find new files not in DB
            foreach (var file in allFiles)
            {
                if (!existingPaths.Contains(file))
                {
                    // Extract folder name and case info from path
                    var folder = Path.GetDirectoryName(file) ?? "";
                    var folderName = new DirectoryInfo(folder).Name;
                    var fileName = Path.GetFileNameWithoutExtension(file);

                    // Try to extract case number and owner from folder name
                    // Format: "01 - owner - 514-1404"
                    var parts = folderName.Split(" - ", 3);
                    var ownerName = parts.Length >= 2 ? parts[1].Trim() : "";
                    var caseNumber = "";
                    var stage = 1;

                    // استخراج شماره پرونده از نام پوشه
                    if (parts.Length >= 3)
                    {
                        var casePart = parts[2].Trim();
                        var caseNumParts = casePart.Split("-");
                        if (caseNumParts.Length == 2)
                        {
                            caseNumber = $"{caseNumParts[1]}/{caseNumParts[0]}";
                        }
                    }

                    // استخراج مرحله از نام فایل
                    if (fileName.Contains("مرحله دوم") || fileName.Contains("مرحله ۲")) stage = 2;
                    else if (fileName.Contains("مرحله سوم") || fileName.Contains("مرحله ۳")) stage = 3;
                    else if (fileName.Contains("مرحله چهارم") || fileName.Contains("مرحله ۴")) stage = 4;

                    // اگه شماره پرونده پیدا نشد، از OwnerName در دیتابیس جستجو کن
                    if (string.IsNullOrEmpty(caseNumber) && !string.IsNullOrEmpty(ownerName))
                    {
                        using var findCmd = _conn.CreateCommand();
                        findCmd.CommandText = "SELECT CaseNumber FROM Cases WHERE Owner = @owner LIMIT 1";
                        findCmd.Parameters.AddWithValue("@owner", ownerName);
                        var result = findCmd.ExecuteScalar();
                        if (result != null) caseNumber = result.ToString() ?? "";
                    }

                    using var cmd = _conn.CreateCommand();
                    cmd.CommandText = "INSERT INTO GeneratedReports (CaseNumber, Stage, TemplateName, OutputPath, CreatedAt, Status, OwnerName, NumberFormat, FolderName, FileExists) VALUES (@cn, @st, @tp, @op, @at, @st2, @on, @nf, @fn, 1)";
                    cmd.Parameters.AddWithValue("@cn", caseNumber);
                    cmd.Parameters.AddWithValue("@st", stage);
                    cmd.Parameters.AddWithValue("@tp", "مکانیک مرحله اول");
                    cmd.Parameters.AddWithValue("@op", file);
                    cmd.Parameters.AddWithValue("@at", File.GetCreationTime(file).ToString("yyyy/MM/dd HH:mm", System.Globalization.CultureInfo.InvariantCulture));
                    cmd.Parameters.AddWithValue("@st2", "synced");
                    cmd.Parameters.AddWithValue("@on", ownerName);
                    cmd.Parameters.AddWithValue("@nf", "Persian");
                    cmd.Parameters.AddWithValue("@fn", folderName);
                    cmd.ExecuteNonQuery();
                    added++;
                }
                else
                {
                    existing++;
                }
            }

            // Find files in DB but not on disk
            foreach (var report in existingReports)
            {
                if (!allFiles.Contains(report.OutputPath))
                {
                    // Mark as missing
                    using var cmd = _conn.CreateCommand();
                    cmd.CommandText = "UPDATE GeneratedReports SET FileExists=0 WHERE OutputPath=@op";
                    cmd.Parameters.AddWithValue("@op", report.OutputPath);
                    cmd.ExecuteNonQuery();
                    removed++;
                }
            }

            return (added, removed, existing);
        }
    }

    /// <summary>Deletes every generated-report log row. Returns the number
    /// of rows deleted. (The Word/PDF files on disk are left alone.)</summary>
    public int DeleteAllGeneratedReports()
    {
        lock (_syncRoot)
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM GeneratedReports";
            return cmd.ExecuteNonQuery();
        }
    }
}
