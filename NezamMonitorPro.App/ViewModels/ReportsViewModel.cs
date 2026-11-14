using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows.Input;
using NezamMonitorPro.Core.Data;
using NezamMonitorPro.Core.Models;
using NezamMonitorPro.Core.Reports;

namespace NezamMonitorPro.App.ViewModels;

public sealed class ReportsViewModel : ViewModelBase
{
    private readonly NezamDatabase _db;
    private string _searchText = "";
    private string _statusMessage = "";
    private string _filterReportType = "همه";
    private string _filterStage = "همه";
    private string _filterOwner = "همه";
    private string _filterCaseNumber = "همه";
    private string _filterEngineer = "همه";
    private string _filterDiscipline = "همه";
    private string _filterSource = "همه";
    private string _filterVisitDate = "همه";

    public ObservableCollection<ReportItem> Reports { get; } = new();
    public string SearchText { get => _searchText; set { SetProperty(ref _searchText, value); ApplyFilters(); } }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public string FilterReportType { get => _filterReportType; set { SetProperty(ref _filterReportType, value); ApplyFilters(); } }
    public string FilterStage { get => _filterStage; set { SetProperty(ref _filterStage, value); ApplyFilters(); } }
    public string FilterOwner { get => _filterOwner; set { SetProperty(ref _filterOwner, value); ApplyFilters(); } }
    public string FilterCaseNumber { get => _filterCaseNumber; set { SetProperty(ref _filterCaseNumber, value); ApplyFilters(); } }
    public string FilterEngineer { get => _filterEngineer; set { SetProperty(ref _filterEngineer, value); ApplyFilters(); } }
    public string FilterDiscipline { get => _filterDiscipline; set { SetProperty(ref _filterDiscipline, value); ApplyFilters(); } }
    public string FilterSource { get => _filterSource; set { SetProperty(ref _filterSource, value); ApplyFilters(); } }
    public string FilterVisitDate { get => _filterVisitDate; set { SetProperty(ref _filterVisitDate, value); ApplyFilters(); } }

    public List<string> ReportTypeFilters { get; set; } = new() { "همه" };
    public List<string> StageFilters { get; set; } = new() { "همه" };
    public List<string> OwnerFilters { get; set; } = new() { "همه" };
    public List<string> CaseNumberFilters { get; set; } = new() { "همه" };
    public List<string> EngineerFilters { get; set; } = new() { "همه" };
    public List<string> DisciplineFilters { get; set; } = new() { "همه" };
    public List<string> SourceFilters { get; } = new() { "همه", "روی سیستم", "در سامانه" };

    private int _totalCount;
    private int _filteredCount;
    public int TotalCount { get => _totalCount; set => SetProperty(ref _totalCount, value); }
    public int FilteredCount { get => _filteredCount; set => SetProperty(ref _filteredCount, value); }
    public string CountDisplay => FilteredCount == TotalCount
        ? $"{TotalCount} مورد"
        : $"{FilteredCount} از {TotalCount} مورد";

    public bool HasActiveFilters => FilterReportType != "همه" || FilterStage != "همه" || FilterOwner != "همه" ||
                                     FilterCaseNumber != "همه" || FilterEngineer != "همه" || FilterDiscipline != "همه" ||
                                     FilterSource != "همه" || FilterVisitDate != "همه" ||
                                     !string.IsNullOrWhiteSpace(SearchText);

    public ICommand RefreshCommand { get; }
    public ICommand ClearFiltersCommand { get; }
    public ICommand FilterByValueCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand OpenReportCommand { get; }
    public ICommand DownloadAndOpenCommand { get; }

    private List<ReportItem> _all = new();

    /// <summary>Case number → 1-based position in the snapshot (case-folder names start with it).</summary>
    private readonly Dictionary<string, int> _caseIndex = new(StringComparer.OrdinalIgnoreCase);

    private string _outputPath = "";

    private static string ResolveOutputPath()
        {
            var exeDir = System.AppDomain.CurrentDomain.BaseDirectory;
            var primary = System.IO.Path.Combine(exeDir, "outputs");
            if (System.IO.Directory.Exists(primary)) return primary;
            // Dev fallback: walk up and look for <repo>/outputs (where real data lives)
            var dir = new System.IO.DirectoryInfo(exeDir);
            for (int i = 0; i < 6 && dir != null; i++)
            {
                var candidate = System.IO.Path.Combine(dir.FullName, "outputs");
                if (System.IO.Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            // Same for nezam_output legacy folder
            dir = new System.IO.DirectoryInfo(exeDir);
            for (int i = 0; i < 6 && dir != null; i++)
            {
                var candidate = System.IO.Path.Combine(dir.FullName, "nezam_output");
                if (System.IO.Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            return primary;
        }

    public ReportsViewModel(NezamDatabase db)
        {
            _db = db;
            _outputPath = ResolveOutputPath();
            RefreshCommand = new RelayCommand(Load);
            ClearFiltersCommand = new RelayCommand(ClearFilters);
            FilterByValueCommand = new RelayCommand<string>(FilterByValue);
            OpenFolderCommand = new RelayCommand<ReportItem>(OpenFolder);
            OpenReportCommand = new RelayCommand<ReportItem>(OpenReport);
            DownloadAndOpenCommand = new RelayCommand<ReportItem>(DownloadAndOpen);
            Load();
        }

    private void Load()
    {
        Reports.Clear();
        var snapshotId = _db.GetActiveSnapshotId();
        if (snapshotId == 0) { StatusMessage = "داده‌ای موجود نیست"; return; }
        var cases = _db.LoadCases(snapshotId);
        _all.Clear();
        _caseIndex.Clear();
        for (int i = 0; i < cases.Count; i++)
            if (!_caseIndex.ContainsKey(cases[i].CaseNumber))
                _caseIndex[cases[i].CaseNumber] = i + 1;

        // (1) Server reports — registered in the سامانه (API data).
        foreach (var c in cases)
            foreach (var r in c.Reports)
                _all.Add(new ReportItem
                {
                    CaseNumber = c.CaseNumber, Owner = c.Owner, ReportType = r.ReportType, Stage = r.Stage,
                    Engineer = r.Engineer, Discipline = r.Discipline, VisitDate = r.VisitDate,
                    Source = "server", RowNo = r.RowNo, ImageName = r.ImageName
                });

        // (2) Local reports — generated on this system (Word/PDF files).
        // Same list as Android's history tab: "روی سیستم" next to "در سامانه".
        var byNumber = new Dictionary<string, Case>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in cases)
            if (!byNumber.ContainsKey(c.CaseNumber)) byNumber[c.CaseNumber] = c;
        foreach (var g in _db.GetGeneratedReports())
        {
            byNumber.TryGetValue(g.CaseNumber, out var c);
            var discipline = c?.Responsibility ?? "";
            var isPdf = string.Equals(g.FileType, "pdf", StringComparison.OrdinalIgnoreCase);
            var preferred = isPdf ? g.PdfPath : g.OutputPath;
            var fallback = isPdf ? g.OutputPath : g.PdfPath;
            var filePath = File.Exists(preferred) ? preferred
                         : File.Exists(fallback) ? fallback
                         : preferred;
            _all.Add(new ReportItem
            {
                CaseNumber = g.CaseNumber,
                Owner = string.IsNullOrWhiteSpace(g.OwnerName) ? (c?.Owner ?? "") : g.OwnerName,
                ReportType = discipline,
                Stage = "مرحله " + StageReportRules.StageName(g.Stage),
                Engineer = "",
                Discipline = discipline,
                VisitDate = g.CreatedAt,
                Source = "local",
                FilePath = filePath
            });
        }

        ReportTypeFilters = FilterHelper.ExtractDistinctValues(_all, r => r.ReportType);
        StageFilters = FilterHelper.ExtractDistinctValues(_all, r => r.Stage);
        OwnerFilters = FilterHelper.ExtractDistinctValues(_all, r => r.Owner);
        CaseNumberFilters = FilterHelper.ExtractDistinctValues(_all, r => r.CaseNumber);
        EngineerFilters = FilterHelper.ExtractDistinctValues(_all, r => r.Engineer);
        DisciplineFilters = FilterHelper.ExtractDistinctValues(_all, r => r.Discipline);
        OnPropertyChanged(nameof(ReportTypeFilters));
        OnPropertyChanged(nameof(StageFilters));
        OnPropertyChanged(nameof(OwnerFilters));
        OnPropertyChanged(nameof(CaseNumberFilters));
        OnPropertyChanged(nameof(EngineerFilters));
        OnPropertyChanged(nameof(DisciplineFilters));

        TotalCount = _all.Count;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var filtered = FilterHelper.ApplyFilters(_all,
            (r => r.ReportType, FilterReportType),
            (r => r.Stage, FilterStage),
            (r => r.Owner, FilterOwner),
            (r => r.CaseNumber, FilterCaseNumber),
            (r => r.Engineer, FilterEngineer),
            (r => r.Discipline, FilterDiscipline),
            // G-006: راست‌کلیک روی ستون «تاریخ» هم فیلتر می‌کند.
            (r => r.VisitDate, FilterVisitDate));

        if (!string.IsNullOrWhiteSpace(SearchText))
            filtered = filtered.Where(r =>
                FilterHelper.Matches(r.Owner, SearchText) ||
                FilterHelper.Matches(r.CaseNumber, SearchText) ||
                FilterHelper.Matches(r.ReportType, SearchText) ||
                FilterHelper.Matches(r.Engineer, SearchText));

        // Source filter: "روی سیستم" = generated here, "در سامانه" = registered on the server.
        if (FilterSource == "روی سیستم") filtered = filtered.Where(r => r.Source == "local");
        else if (FilterSource == "در سامانه") filtered = filtered.Where(r => r.Source == "server");

        Reports.Clear();
        foreach (var item in filtered) Reports.Add(item);
        FilteredCount = Reports.Count;
        OnPropertyChanged(nameof(CountDisplay));
        OnPropertyChanged(nameof(HasActiveFilters));
        StatusMessage = CountDisplay;
    }

    private void ClearFilters()
    {
        SearchText = ""; FilterReportType = "همه"; FilterStage = "همه";
        FilterOwner = "همه"; FilterCaseNumber = "همه"; FilterEngineer = "همه"; FilterDiscipline = "همه";
        FilterSource = "همه"; FilterVisitDate = "همه";
    }

    private void FilterByValue(string? parameter)
    {
        if (string.IsNullOrWhiteSpace(parameter)) return;
        var parts = parameter.Split(':', 2);
        if (parts.Length != 2) return;
        switch (parts[0])
        {
            case "ReportType": FilterReportType = parts[1]; break;
            case "Stage": FilterStage = parts[1]; break;
            case "Owner": FilterOwner = parts[1]; break;
            case "CaseNumber": FilterCaseNumber = parts[1]; break;
            case "Engineer": FilterEngineer = parts[1]; break;
            case "Discipline": FilterDiscipline = parts[1]; break;
            // G-006: «منبع» مقدار نمایشی دارد (🖥 روی سیستم / 🌐 در سامانه)؛
            // به مقدار فیلتر («روی سیستم» / «در سامانه») نگاشت می‌شود.
            case "Source": FilterSource = parts[1] switch
            {
                "🖥 روی سیستم" => "روی سیستم",
                "🌐 در سامانه" => "در سامانه",
                _ => parts[1]
            }; break;
            case "VisitDate": FilterVisitDate = parts[1]; break;
        }
    }

    public void RemoveFilter(string column)
        {
            switch (column)
            {
                case "ReportType": FilterReportType = "همه"; break;
                case "Stage": FilterStage = "همه"; break;
                case "Owner": FilterOwner = "همه"; break;
                case "CaseNumber": FilterCaseNumber = "همه"; break;
                case "Engineer": FilterEngineer = "همه"; break;
                case "Discipline": FilterDiscipline = "همه"; break;
                case "Source": FilterSource = "همه"; break;
                case "VisitDate": FilterVisitDate = "همه"; break;
            }
        }

        private void OpenFolder(ReportItem? item)
        {
            if (item == null)
            {
                StatusMessage = "موردی انتخاب نشده";
                return;
            }

            if (string.IsNullOrEmpty(item.CaseNumber))
            {
                StatusMessage = "شماره پرونده نامعتبر";
                return;
            }

            // Local report: open the folder of its own file.
            if (item.Source == "local" && !string.IsNullOrEmpty(item.FilePath))
            {
                var dir = System.IO.Path.GetDirectoryName(item.FilePath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
                    StatusMessage = $"باز شد: {dir}";
                    return;
                }
            }

            var folder = FindCaseFolder(item.CaseNumber);
            if (folder != null)
            {
                Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
                StatusMessage = $"باز شد: {folder}";
                return;
            }

            StatusMessage = !Directory.Exists(_outputPath)
                ? "پوشه خروجی یافت نشد"
                : $"پوشه‌ای برای پرونده {item.CaseNumber} یافت نشد";
        }

        /// <summary>
        /// View a report: local files open directly; server reports open
        /// the image downloaded during extraction (outputs/&lt;case&gt;/&lt;rowNo&gt;_&lt;type&gt;.&lt;ext&gt;).
        /// </summary>
        private void OpenReport(ReportItem? item)
        {
            if (item == null)
            {
                StatusMessage = "موردی انتخاب نشده";
                return;
            }

            if (item.Source == "local")
            {
                if (!string.IsNullOrEmpty(item.FilePath) && File.Exists(item.FilePath))
                {
                    Process.Start(new ProcessStartInfo { FileName = item.FilePath, UseShellExecute = true });
                    StatusMessage = $"باز شد: {System.IO.Path.GetFileName(item.FilePath)}";
                }
                else
                {
                    StatusMessage = "فایل گزارش روی سیستم نیست — شاید جابه‌جا یا حذف شده";
                }
                return;
            }

            // Server report: resolve the downloaded image file.
            var file = ResolveServerFile(item);
            if (file != null)
            {
                Process.Start(new ProcessStartInfo { FileName = file, UseShellExecute = true });
                StatusMessage = $"باز شد: {System.IO.Path.GetFileName(file)}";
                return;
            }

            var folder = FindCaseFolder(item.CaseNumber);
            if (folder != null)
            {
                Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
                StatusMessage = "فایل تصویر هنوز دانلود نشده؛ پوشه پرونده باز شد";
                return;
            }

            StatusMessage = string.IsNullOrEmpty(item.ImageName)
                ? "این گزارش در سامانه فایل تصویری ندارد"
                : "پوشه پرونده یافت نشد — بعد از بروزرسانی دوباره امتحان کن";
        }

        /// <summary>
        /// Download a server report's image now (same URL and file name the
        /// extraction uses) and open it. If it is already on disk, just opens it.
        /// </summary>
        private async void DownloadAndOpen(ReportItem? item)
        {
            if (item == null)
            {
                StatusMessage = "موردی انتخاب نشده";
                return;
            }

            // Local reports are already on disk — just open them.
            if (item.Source != "server")
            {
                OpenReport(item);
                return;
            }

            if (string.IsNullOrEmpty(item.ImageName))
            {
                StatusMessage = "این گزارش در سامانه فایل تصویری ندارد";
                return;
            }

            // Already downloaded? Just open it.
            var existing = ResolveServerFile(item);
            if (existing != null)
            {
                Process.Start(new ProcessStartInfo { FileName = existing, UseShellExecute = true });
                StatusMessage = $"باز شد: {System.IO.Path.GetFileName(existing)}";
                return;
            }

            // Find the case folder, or create it with the extractor's naming.
            var folder = FindCaseFolder(item.CaseNumber);
            if (folder == null)
            {
                if (!_caseIndex.TryGetValue(item.CaseNumber, out var idx))
                {
                    StatusMessage = "پرونده یافت نشد — بعد از بروزرسانی دوباره امتحان کن";
                    return;
                }
                var safeOwner = (item.Owner ?? "").Replace("/", "_").Replace("\\", "_").Replace(":", "_");
                var safeCaseNum = (item.CaseNumber ?? "").Replace("/", "_");
                folder = System.IO.Path.Combine(_outputPath, $"{idx}_{safeOwner}_{safeCaseNum}");
                Directory.CreateDirectory(folder);
            }

            var username = _db.GetSetting(SettingKeys.Username);
            var password = _db.GetSetting(SettingKeys.Password);
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                StatusMessage = "برای دانلود، اول در صفحه بروزرسانی با نام کاربری و رمز عبور وارد شو";
                return;
            }

            StatusMessage = "در حال دانلود گزارش...";
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
                var payload = JsonSerializer.Serialize(new { ozv_num = username, ozv_pass = password, ozv_type = 0 });
                var loginReq = new HttpRequestMessage(HttpMethod.Post, "http://service.yazdnezam.ir:8033/panel/api/login")
                { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
                var loginResp = await http.SendAsync(loginReq);
                if (!loginResp.IsSuccessStatusCode) { StatusMessage = "ورود به سامانه ناموفق بود"; return; }
                var token = JsonDocument.Parse(await loginResp.Content.ReadAsStringAsync()).RootElement.GetProperty("token").GetString()!;
                http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(token);

                var safeReportType = (item.ReportType ?? "").Replace("/", "_").Replace("\\", "_").Replace(":", "_");
                var ext = System.IO.Path.GetExtension(item.ImageName).ToLower();
                if (string.IsNullOrEmpty(ext)) ext = ".jpg";
                var downloadUrl = $"http://service.yazdnezam.ir:8033/panel/Panel/public/img/gozaresh/{item.ImageName}";
                var fileName = $"{item.RowNo}_{safeReportType}{ext}";
                var filePath = System.IO.Path.Combine(folder, fileName);
                var resp = await http.GetAsync(downloadUrl);
                if (!resp.IsSuccessStatusCode) { StatusMessage = "دانلود گزارش ناموفق بود"; return; }
                var bytes = await resp.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(filePath, bytes);
                Process.Start(new ProcessStartInfo { FileName = filePath, UseShellExecute = true });
                StatusMessage = $"دانلود و باز شد: {fileName}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"دانلود ناموفق: {ex.Message}";
            }
        }

        /// <summary>
        /// Find the case's output folder. The extractor names folders
        /// "{n}_{owner}_{caseNumber with '/' → '_'}", e.g. "3_..._1400_123".
        /// </summary>
        private string? FindCaseFolder(string caseNumber)
        {
            if (!Directory.Exists(_outputPath)) return null;
            var safe = caseNumber.Replace("/", "_");
            var parts = caseNumber.Split('/');
            var legacy = parts.Length == 2 ? $"{parts[1]}-{parts[0]}" : caseNumber;
            foreach (var dir in Directory.GetDirectories(_outputPath))
            {
                var name = System.IO.Path.GetFileName(dir);
                if (name.Contains(safe, StringComparison.OrdinalIgnoreCase) ||
                    name.Contains(legacy, StringComparison.OrdinalIgnoreCase))
                    return dir;
            }
            return null;
        }

        /// <summary>
        /// Downloaded server images are named "{RowNo}_{reportType}.{ext}"
        /// inside the case folder (RowNo is the 1-based API order).
        /// </summary>
        private string? ResolveServerFile(ReportItem item)
        {
            if (string.IsNullOrEmpty(item.ImageName)) return null;
            var folder = FindCaseFolder(item.CaseNumber);
            if (folder == null) return null;
            var prefix = item.RowNo + "_";
            foreach (var file in Directory.GetFiles(folder))
            {
                if (System.IO.Path.GetFileName(file).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return file;
            }
            return null;
        }
    }

public sealed class ReportItem
{
    public string CaseNumber { get; set; } = "";
    public string Owner { get; set; } = "";
    public string ReportType { get; set; } = "";
    public string Stage { get; set; } = "";
    public string Engineer { get; set; } = "";
    public string Discipline { get; set; } = "";
    public string VisitDate { get; set; } = "";
    /// <summary>"local" = generated on this system, "server" = registered in the سامانه.</summary>
    public string Source { get; set; } = "server";
    public string SourceDisplay => Source == "local" ? "🖥 روی سیستم" : "🌐 در سامانه";
    /// <summary>Local file to open (Word/PDF). Empty for server reports — resolved on demand.</summary>
    public string FilePath { get; set; } = "";
    /// <summary>1-based API order; the downloaded image file starts with "{RowNo}_".</summary>
    public string RowNo { get; set; } = "";
    public string ImageName { get; set; } = "";
}
