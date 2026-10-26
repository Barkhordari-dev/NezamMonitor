using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using System.IO;
using NezamMonitorPro.Core;
using NezamMonitorPro.Core.Data;
using NezamMonitorPro.Core.Models;
using NezamMonitorPro.Core.Reports;
using NezamMonitorPro.App.Services;

namespace NezamMonitorPro.App.ViewModels;

public sealed class GeneratorViewModel : ViewModelBase
{
    private readonly NezamDatabase _db;
    private string _templatePath = "";
    private string _outputPath = "";
    private string _statusMessage = "مرحله و تمپلیت را انتخاب کنید";
    private int _progress;
    private string _progressText = "";
    private int _selectedStage = 1;
    private string _selectedTemplateName = "";
    private string _selectedTemplatePath = "";
    private string _reportDate = "";
    private bool _isHistoryExpanded = false;
    private string _historySearchText = "";
    private int _historyFilterStage = 0;
    private int _historyFilterFormat = 0;
    private string _previewText = "";
    private bool _useLtr = false;
    private int _activeTab = 0;

    public string TemplatePath { get => _templatePath; set => SetProperty(ref _templatePath, value); }
    public string OutputPath { get => _outputPath; set => SetProperty(ref _outputPath, value); }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public int Progress { get => _progress; set => SetProperty(ref _progress, value); }
    public string ProgressText { get => _progressText; set => SetProperty(ref _progressText, value); }

    public int SelectedStage
    {
        get => _selectedStage;
        set
        {
            if (SetProperty(ref _selectedStage, value))
                AutoSelectTemplate();
        }
    }

    public string SelectedTemplateName
    {
        get => _selectedTemplateName;
        set => SetProperty(ref _selectedTemplateName, value);
    }

    public string SelectedTemplatePath
    {
        get => _selectedTemplatePath;
        set => SetProperty(ref _selectedTemplatePath, value);
    }

    public string ReportDate
    {
        get => _reportDate;
        set => SetProperty(ref _reportDate, value);
    }

    public bool IsHistoryExpanded
    {
        get => _isHistoryExpanded;
        set => SetProperty(ref _isHistoryExpanded, value);
    }

    public string HistorySearchText
    {
        get => _historySearchText;
        set
        {
            if (SetProperty(ref _historySearchText, value))
                LoadHistory();
        }
    }

    public int HistoryFilterStage
    {
        get => _historyFilterStage;
        set
        {
            if (SetProperty(ref _historyFilterStage, value))
                LoadHistory();
        }
    }

    public int HistoryFilterFormat
    {
        get => _historyFilterFormat;
        set
        {
            if (SetProperty(ref _historyFilterFormat, value))
                LoadHistory();
        }
    }

    public string PreviewText { get => _previewText; set => SetProperty(ref _previewText, value); }
    public bool UseLtr { get => _useLtr; set => SetProperty(ref _useLtr, value); }
    public int ActiveTab { get => _activeTab; set => SetProperty(ref _activeTab, value); }

    public ObservableCollection<CaseReportItem> ReportStatus { get; } = new();
    public ObservableCollection<TemplateInfo> AvailableTemplates { get; } = new();
    public ObservableCollection<string> TemplateList
    {
        get
        {
            var list = new ObservableCollection<string>(AvailableTemplates.Select(t => t.FileName));
            return list;
        }
    }

    private int _filterStage = 0;
    private int _filterStatus = 0;
    private List<CaseReportItem> _allReportItems = new();

    // کش مقایسه اسکن‌ها از دیتابیس
        private Dictionary<string, bool> _previousScanState = new();
            private bool _hasLoadedFromDb = false;
            private CancellationTokenSource? _scanCts;

            // زمان‌بندی - آرایه ماه‌ها برای هر گروه و هر مرحله (اندیس ۱-۴ استفاده می‌شود)
        private readonly int[] _groupAMonths = new int[5]; // الف: ۳ مرحله
        private readonly int[] _groupBMonths = new int[5]; // ب: ۴ مرحله
        private readonly int[] _groupCMonths = new int[5]; // ج: ۴ مرحله
        private readonly int[] _groupDMonths = new int[5]; // د: ۴ مرحله

        // Properties for each stage's months per group (for direct binding if needed)
        public int GroupAStage1Months { get => _groupAMonths[1]; set { if (SetProperty(ref _groupAMonths[1], value)) { _db.SaveSetting("report_group_a_stage_1_months", value.ToString()); ApplyReportFilters(); } } }
        public int GroupAStage2Months { get => _groupAMonths[2]; set { if (SetProperty(ref _groupAMonths[2], value)) { _db.SaveSetting("report_group_a_stage_2_months", value.ToString()); ApplyReportFilters(); } } }
        public int GroupAStage3Months { get => _groupAMonths[3]; set { if (SetProperty(ref _groupAMonths[3], value)) { _db.SaveSetting("report_group_a_stage_3_months", value.ToString()); ApplyReportFilters(); } } }

        public int GroupBStage1Months { get => _groupBMonths[1]; set { if (SetProperty(ref _groupBMonths[1], value)) { _db.SaveSetting("report_group_b_stage_1_months", value.ToString()); ApplyReportFilters(); } } }
        public int GroupBStage2Months { get => _groupBMonths[2]; set { if (SetProperty(ref _groupBMonths[2], value)) { _db.SaveSetting("report_group_b_stage_2_months", value.ToString()); ApplyReportFilters(); } } }
        public int GroupBStage3Months { get => _groupBMonths[3]; set { if (SetProperty(ref _groupBMonths[3], value)) { _db.SaveSetting("report_group_b_stage_3_months", value.ToString()); ApplyReportFilters(); } } }
        public int GroupBStage4Months { get => _groupBMonths[4]; set { if (SetProperty(ref _groupBMonths[4], value)) { _db.SaveSetting("report_group_b_stage_4_months", value.ToString()); ApplyReportFilters(); } } }

        public int GroupCStage1Months { get => _groupCMonths[1]; set { if (SetProperty(ref _groupCMonths[1], value)) { _db.SaveSetting("report_group_c_stage_1_months", value.ToString()); ApplyReportFilters(); } } }
        public int GroupCStage2Months { get => _groupCMonths[2]; set { if (SetProperty(ref _groupCMonths[2], value)) { _db.SaveSetting("report_group_c_stage_2_months", value.ToString()); ApplyReportFilters(); } } }
        public int GroupCStage3Months { get => _groupCMonths[3]; set { if (SetProperty(ref _groupCMonths[3], value)) { _db.SaveSetting("report_group_c_stage_3_months", value.ToString()); ApplyReportFilters(); } } }
        public int GroupCStage4Months { get => _groupCMonths[4]; set { if (SetProperty(ref _groupCMonths[4], value)) { _db.SaveSetting("report_group_c_stage_4_months", value.ToString()); ApplyReportFilters(); } } }

        public int GroupDStage1Months { get => _groupDMonths[1]; set { if (SetProperty(ref _groupDMonths[1], value)) { _db.SaveSetting("report_group_d_stage_1_months", value.ToString()); ApplyReportFilters(); } } }
        public int GroupDStage2Months { get => _groupDMonths[2]; set { if (SetProperty(ref _groupDMonths[2], value)) { _db.SaveSetting("report_group_d_stage_2_months", value.ToString()); ApplyReportFilters(); } } }
        public int GroupDStage3Months { get => _groupDMonths[3]; set { if (SetProperty(ref _groupDMonths[3], value)) { _db.SaveSetting("report_group_d_stage_3_months", value.ToString()); ApplyReportFilters(); } } }
        public int GroupDStage4Months { get => _groupDMonths[4]; set { if (SetProperty(ref _groupDMonths[4], value)) { _db.SaveSetting("report_group_d_stage_4_months", value.ToString()); ApplyReportFilters(); } } }

        // لیست ماه‌ها برای ComboBox (پیش‌فرض‌ها تا ۳۰ ماه بر اساس سند)
            public List<int> MonthOptions { get; } = Enumerable.Range(1, 30).ToList();

        // زمان‌بندی UI Properties
        public ObservableCollection<string> BuildingGroups { get; } = new() { "الف", "ب", "ج", "د" };
        public ObservableCollection<string> EngineeringDisciplines { get; } = new() { "عمران", "معماری", "برق", "مکانیک", "نقشه‌برداری", "شهرسازی" };

        private string _selectedBuildingGroup = "الف";
        public string SelectedBuildingGroup
        {
            get => _selectedBuildingGroup;
            set
            {
                if (SetProperty(ref _selectedBuildingGroup, value))
                {
                    UpdateStageConfigurations();
                }
            }
        }

        private string _selectedEngineeringDiscipline = "مکانیک";
        public string SelectedEngineeringDiscipline
        {
            get => _selectedEngineeringDiscipline;
            set
            {
                if (SetProperty(ref _selectedEngineeringDiscipline, value))
                {
                    UpdateStageConfigurations();
                }
            }
        }

        public ObservableCollection<StageConfiguration> StageConfigurations { get; } = new();

            private bool _isScanning;
            public bool IsScanning
            {
                get => _isScanning;
                private set => SetProperty(ref _isScanning, value);
            }

            public ICommand CancelScanCommand { get; }
            public ICommand ResetToDefaultsCommand { get; }
            public ICommand SaveStageConfigurationCommand { get; }

    public int FilterStage
    {
        get => _filterStage;
        set
        {
            if (SetProperty(ref _filterStage, value))
                ApplyReportFilters();
        }
    }

    public int FilterStatus
    {
        get => _filterStatus;
        set
        {
            if (SetProperty(ref _filterStatus, value))
                ApplyReportFilters();
        }
    }

    public string CountDisplay => $"تعداد: {ReportStatus.Count(n => n.IsSelected)} از {ReportStatus.Count}";

    public ICommand BrowseTemplateCommand { get; }
    public ICommand BrowseOutputCommand { get; }
    public ICommand ScanCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand DeselectAllCommand { get; }
    public ICommand GenerateCommand { get; }
    public ICommand PreviewCommand { get; }
    public ICommand ToggleHistoryCommand { get; }
    public ICommand RefreshHistoryCommand { get; }
    public ICommand OpenReportCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand RegenerateCommand { get; }
    public ICommand DeleteReportCommand { get; }
    public ICommand SwitchToGenerateCommand { get; }
    public ICommand SwitchToHistoryCommand { get; }
    public ICommand DeleteLogCommand { get; }
    public ICommand SyncOutputCommand { get; }

    public bool HasActiveFilters => !string.IsNullOrEmpty(HistorySearchText) || HistoryFilterStage > 0 || HistoryFilterFormat > 0;

    public GeneratorViewModel(NezamDatabase db)
            {
                _db = db;
                var exeDir = DatabaseService.GetAppDirectory();
                _templatePath = Path.Combine(exeDir, "templates");
                _outputPath = Path.Combine(exeDir, "outputs");
                _reportDate = PersianDateHelper.ToPersianDateDigits(DateTime.Now);

            // بارگذاری تنظیمات زمان‌بندی جدید (بر اساس مرحله و گروه)
            LoadSchedulingSettings();

            BrowseTemplateCommand = new RelayCommand(BrowseTemplate);
                        BrowseOutputCommand = new RelayCommand(BrowseOutput);
                        ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsScanning);
                                    CancelScanCommand = new RelayCommand(() => _scanCts?.Cancel(), () => IsScanning);
                                    SelectAllCommand = new RelayCommand(SelectAll);
                        DeselectAllCommand = new RelayCommand(DeselectAll);
                        GenerateCommand = new RelayCommand(Generate);
                        PreviewCommand = new RelayCommand(Preview);
                        ToggleHistoryCommand = new RelayCommand(() => IsHistoryExpanded = !IsHistoryExpanded);
                        RefreshHistoryCommand = new RelayCommand(LoadHistory);
                        OpenReportCommand = new RelayCommand<ReportHistoryItem>(OpenReport);
                        OpenFolderCommand = new RelayCommand<ReportHistoryItem>(OpenFolder);
                        RegenerateCommand = new RelayCommand<ReportHistoryItem>(RegenerateWithConfirmation);
                        DeleteReportCommand = new RelayCommand<ReportHistoryItem>(DeleteReportWithConfirmation);
                        SwitchToGenerateCommand = new RelayCommand(() => ActiveTab = 0);
                        SwitchToHistoryCommand = new RelayCommand(() => { ActiveTab = 1; LoadHistory(); });
                        DeleteLogCommand = new RelayCommand<ReportHistoryItem>(DeleteLog);
                        SyncOutputCommand = new RelayCommand(SyncOutputFolder);
                        ResetToDefaultsCommand = new RelayCommand(ResetSchedulingDefaults);
                        SaveStageConfigurationCommand = new RelayCommand<int>(SaveStageConfiguration);

            LoadTemplates();
            LoadHistory();
            LoadPreviousScanResults();
            UpdateStageConfigurations();
        }

        private void LoadSchedulingSettings()
                {
                    // Group A (3 stages)
                    _groupAMonths[1] = int.TryParse(_db.GetSetting("report_group_a_stage_1_months"), out var a1) && a1 >= 1 && a1 <= 30 ? a1 : 2;
                    _groupAMonths[2] = int.TryParse(_db.GetSetting("report_group_a_stage_2_months"), out var a2) && a2 >= 1 && a2 <= 30 ? a2 : 6;
                    _groupAMonths[3] = int.TryParse(_db.GetSetting("report_group_a_stage_3_months"), out var a3) && a3 >= 1 && a3 <= 30 ? a3 : 10;

                    // Group B (4 stages)
                    _groupBMonths[1] = int.TryParse(_db.GetSetting("report_group_b_stage_1_months"), out var b1) && b1 >= 1 && b1 <= 30 ? b1 : 3;
                    _groupBMonths[2] = int.TryParse(_db.GetSetting("report_group_b_stage_2_months"), out var b2) && b2 >= 1 && b2 <= 30 ? b2 : 6;
                    _groupBMonths[3] = int.TryParse(_db.GetSetting("report_group_b_stage_3_months"), out var b3) && b3 >= 1 && b3 <= 30 ? b3 : 12;
                    _groupBMonths[4] = int.TryParse(_db.GetSetting("report_group_b_stage_4_months"), out var b4) && b4 >= 1 && b4 <= 30 ? b4 : 20;

                    // Group C (4 stages)
                    _groupCMonths[1] = int.TryParse(_db.GetSetting("report_group_c_stage_1_months"), out var c1) && c1 >= 1 && c1 <= 30 ? c1 : 3;
                    _groupCMonths[2] = int.TryParse(_db.GetSetting("report_group_c_stage_2_months"), out var c2) && c2 >= 1 && c2 <= 30 ? c2 : 8;
                    _groupCMonths[3] = int.TryParse(_db.GetSetting("report_group_c_stage_3_months"), out var c3) && c3 >= 1 && c3 <= 30 ? c3 : 16;
                    _groupCMonths[4] = int.TryParse(_db.GetSetting("report_group_c_stage_4_months"), out var c4) && c4 >= 1 && c4 <= 30 ? c4 : 24;

                    // Group D (4 stages)
                    _groupDMonths[1] = int.TryParse(_db.GetSetting("report_group_d_stage_1_months"), out var d1) && d1 >= 1 && d1 <= 30 ? d1 : 3;
                    _groupDMonths[2] = int.TryParse(_db.GetSetting("report_group_d_stage_2_months"), out var d2) && d2 >= 1 && d2 <= 30 ? d2 : 8;
                    _groupDMonths[3] = int.TryParse(_db.GetSetting("report_group_d_stage_3_months"), out var d3) && d3 >= 1 && d3 <= 30 ? d3 : 16;
                    _groupDMonths[4] = int.TryParse(_db.GetSetting("report_group_d_stage_4_months"), out var d4) && d4 >= 1 && d4 <= 30 ? d4 : 30;

                    // Per‑group migration: if a group’s stage‑1 setting is missing, copy the old group‑wide setting (if any)
                    int oldA = 2;
                    if (!string.IsNullOrEmpty(_db.GetSetting("report_interval_group_a")))
                    {
                        if (int.TryParse(_db.GetSetting("report_interval_group_a"), out var parsed) && parsed >= 1 && parsed <= 30)
                            oldA = parsed;
                    }
                    int oldB = 3;
                    if (!string.IsNullOrEmpty(_db.GetSetting("report_interval_group_b")))
                    {
                        if (int.TryParse(_db.GetSetting("report_interval_group_b"), out var parsed) && parsed >= 1 && parsed <= 30)
                            oldB = parsed;
                    }

                    // Group A
                    if (string.IsNullOrEmpty(_db.GetSetting("report_group_a_stage_1_months")))
                    {
                        _groupAMonths[1] = oldA;
                        _groupAMonths[2] = oldA;
                        _groupAMonths[3] = oldA;
                        // persist so we don’t migrate again
                        _db.SaveSetting("report_group_a_stage_1_months", oldA.ToString());
                        _db.SaveSetting("report_group_a_stage_2_months", oldA.ToString());
                        _db.SaveSetting("report_group_a_stage_3_months", oldA.ToString());
                    }

                    // Group B
                    if (string.IsNullOrEmpty(_db.GetSetting("report_group_b_stage_1_months")))
                    {
                        _groupBMonths[1] = oldB;
                        _groupBMonths[2] = oldB;
                        _groupBMonths[3] = oldB;
                        _groupBMonths[4] = oldB;
                        _db.SaveSetting("report_group_b_stage_1_months", oldB.ToString());
                        _db.SaveSetting("report_group_b_stage_2_months", oldB.ToString());
                        _db.SaveSetting("report_group_b_stage_3_months", oldB.ToString());
                        _db.SaveSetting("report_group_b_stage_4_months", oldB.ToString());
                    }
                    // Groups C and D have no legacy settings; keep defaults.
                }

    private void LoadTemplates()
    {
        AvailableTemplates.Clear();
        var generator = CreateGenerator();
        foreach (var t in generator.ListTemplates())
            AvailableTemplates.Add(t);
        AutoSelectTemplate();
    }

    /// <summary>
    /// ساخت مولد گزارش با تمام مسیرهای قالب: پوشهٔ خود برنامه + مسیرهای نسخه‌های قبلی
    /// (مثل Downloads\NezamMonitor-Windows\templates) + مسیرهای قابل‌تنظیم در پایگاه‌داده.
    /// </summary>
    private StageReportGenerator CreateGenerator(string? primaryDir = null)
    {
        var generator = new StageReportGenerator(primaryDir ?? _templatePath);

        foreach (var dir in GetExtraTemplateDirectories())
            generator.AddTemplateDirectory(dir);

        return generator;
    }

    private IEnumerable<string> GetExtraTemplateDirectories()
    {
        var dirs = new List<string>();

        try
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(profile))
            {
                dirs.Add(Path.Combine(profile, "Downloads", "NezamMonitor-Windows", "templates"));
                dirs.Add(Path.Combine(profile, "Downloads", "NezamMonitor-Windows", "Templates"));
                dirs.Add(Path.Combine(profile, "Downloads", "NezamMonitor-Windows"));
                dirs.Add(Path.Combine(profile, "Downloads", "templates"));
            }

            // مسیر نسخهٔ قبلی/سورس روی درایو E
            dirs.Add(@"E:\Nezam Monitor-Mahdi2\Source_and_Development\templates");
            dirs.Add(@"E:\NezamMonitor-Mahdi2\Source_and_Development\templates");
            dirs.Add(@"E:\Nezam Monitor-Mahdi2\templates");
        }
        catch { }

        // مسیر دلخواه کاربر: template_paths=D:\a\templates;D:\b\templates
        try
        {
            var extra = _db.GetSetting("template_paths");
            if (!string.IsNullOrWhiteSpace(extra))
            {
                foreach (var part in extra.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    dirs.Add(part);
            }
        }
        catch { }

        return dirs;
    }

    private void AutoSelectTemplate()
    {
        var match = AvailableTemplates.FirstOrDefault(t => t.Stage == SelectedStage);
        if (match != null)
        {
            SelectedTemplateName = match.FileName;
            SelectedTemplatePath = match.Path;
        }
        else
        {
            SelectedTemplateName = "";
            SelectedTemplatePath = "";
        }
    }

    private void BrowseTemplate()
    {
        var dialog = new OpenFileDialog
        {
            Title = "انتخاب تمپلیت",
            Filter = "فایل Word|*.docx",
            InitialDirectory = _templatePath
        };
        if (dialog.ShowDialog() == true)
        {
            SelectedTemplatePath = dialog.FileName;
        }
    }

    private void BrowseOutput()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "انتخاب پوشه خروجی",
            InitialDirectory = _outputPath
        };
        if (dialog.ShowDialog() == true)
        {
            _outputPath = dialog.FolderName;
        }
    }

    /// <summary>
    /// بارگذاری نتایج اسکن قبلی از دیتابیس (فقط برای نمایش اولیه)
    /// </summary>
    private void LoadPreviousScanResults()
    {
        try
        {
            var saved = _db.LoadLastScanResults();
            if (saved.Count == 0) return; // هنوز اسکنی انجام نشده

            // --- Build row-number map ONCE (O(N), not O(N²)) ---
            var cases = _db.LoadCases(_db.GetActiveSnapshotId());
            var rowMap = new Dictionary<string, int>(cases.Count);
            for (int i = 0; i < cases.Count; i++)
                rowMap[cases[i].CaseNumber] = i + 1; // 1-based, matches CasesView order

            _allReportItems.Clear();
            ReportStatus.Clear();

            foreach (var s in saved)
            {
                var shamsi = !string.IsNullOrWhiteSpace(s.GeneratedAtShamsi) ? s.GeneratedAtShamsi : PersianDateHelper.GregoryStringToShamsi(s.GeneratedAt);
                var item = new CaseReportItem
                {
                    CaseNumber = s.CaseNumber,
                    RowNumber = rowMap.TryGetValue(s.CaseNumber, out var rn) ? rn : 0,
                    Stage = s.Stage,
                    Owner = s.Owner,
                    FullAddress = s.FullAddress,
                    PermitNumber = s.PermitNumber,
                    Companion = s.Companion,
                    StageName = s.StageName,
                    Status = s.Status,
                    GeneratedAt = s.GeneratedAt,
                    GeneratedAtShamsi = shamsi,
                    PermitDate = s.PermitDate,
                    Deadline = s.Deadline,
                    DaysRemaining = s.DaysRemaining,
                    DaysRemainingSort = CalculateDaysRemainingSort(s.Deadline),
                    DeadlineStatus = s.DeadlineStatus,
                    BuildingGroup = s.BuildingGroup,
                    IsChanged = false, // در لود اولیه تغییری نشون داده نمیشه
                };
                _allReportItems.Add(item);
            }
            ApplyReportFilters();
            StatusMessage = $"نتایج اسکن قبلی بارگذاری شد ({_allReportItems.Count} مورد). اسکن مجدد برای بروزرسانی.";
        }
        catch { /* silently ignore - scan will show fresh results */ }
    }

    /// <summary>
        /// اسکن پرونده‌ها و نمایش وضعیت واقعی گزارش‌ها (Async با پشتیبانی از لغو)
        /// </summary>
        private async Task ScanAsync()
        {
            _scanCts = new CancellationTokenSource();
            var ct = _scanCts.Token;
        
            IsScanning = true;
            StatusMessage = "در حال اسکن...";
            Progress = 0;
            ProgressText = "";

            try
            {
                await Task.Run(() => DoScan(ct), ct);
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "اسکن لغو شد";
                Progress = 0;
                return;
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطا: {ex.Message}";
                Progress = 0;
                return;
            }
            finally
            {
                IsScanning = false;
                _scanCts?.Dispose();
                _scanCts = null;
            }
        }

        private void DoScan(CancellationToken ct)
        {
            // Clear on UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                _allReportItems.Clear();
                ReportStatus.Clear();
            });

            Directory.CreateDirectory(_outputPath);
            var cases = _db.LoadCases(_db.GetActiveSnapshotId());

            // ─── بارگذاری کش از DB (فقط یک بار) ───
            if (!_hasLoadedFromDb)
            {
                _previousScanState = _db.LoadScanCache();
                _hasLoadedFromDb = true;
            }

            // دریافت تمام گزارش‌های تولید شده از دیتابیس
            var existingReports = _db.GetGeneratedReports();
            var generatedDates = new Dictionary<string, string>();
            foreach (var r in existingReports)
            {
                var key = $"{r.CaseNumber}|{r.Stage}";
                if (!generatedDates.ContainsKey(key) && !string.IsNullOrEmpty(r.CreatedAt))
                    generatedDates[key] = r.CreatedAt;
            }

            // همچنین اسکن فایل‌های واقعی در پوشه outputs
            var actualFiles = new HashSet<string>(); // "CaseNumber|Stage"
            if (Directory.Exists(_outputPath))
            {
                foreach (var dir in Directory.GetDirectories(_outputPath))
                {
                    ct.ThrowIfCancellationRequested();
                    var dirName = new DirectoryInfo(dir).Name;
                    var dashParts = dirName.Split(" - ", 3);
                    var casePart = dashParts.Length >= 3 ? dashParts[2].Trim() : "";
                    if (string.IsNullOrEmpty(casePart)) continue;

                    var caseNumParts = casePart.Split("-");
                    if (caseNumParts.Length == 2)
                    {
                        var dbCaseNumber = $"{caseNumParts[1]}/{caseNumParts[0]}";
                        var persianCase = DigitNormalizer.ToPersianDigits(dbCaseNumber);

                        foreach (var file in Directory.GetFiles(dir, "*.docx"))
                        {
                            var fileName = Path.GetFileNameWithoutExtension(file);
                            for (int stage = 1; stage <= 4; stage++)
                            {
                                var stageWord = StageReportRules.StageName(stage);
                                if (fileName.Contains($"مرحله {stageWord}"))
                                {
                                    actualFiles.Add($"{dbCaseNumber}|{stage}");
                                    actualFiles.Add($"{persianCase}|{stage}");
                                }
                            }
                        }
                    }
                }
            }

            // ─── کش جدید برای ذخیره وضعیت فعلی ───
            var currentScanState = new Dictionary<string, bool>();
            bool isFirstScan = _previousScanState.Count == 0;

            // Build row-number map ONCE (O(N), not O(N²))
            var rowMap = new Dictionary<string, int>(cases.Count);
            for (int i = 0; i < cases.Count; i++)
                rowMap[cases[i].CaseNumber] = i + 1;

            int current = 0;
            foreach (var c in cases)
            {
                ct.ThrowIfCancellationRequested();
                current++;
            
                // Update progress on UI thread
                var progress = (int)((double)current / cases.Count * 100);
                var progressText = $"{current}/{cases.Count}";
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Progress = progress;
                    ProgressText = progressText;
                });

                // تعیین تعداد مراحل بر اساس حق‌الزحمه
                var feeStages = c.Fees.Where(f => f.Stage != "0" && f.Stage != "۰")
                                       .Select(f => f.Stage)
                                       .Distinct()
                                       .Count();
            
                // گروه ساختمانی
                var buildingGroup = c.Specification?.BuildingGroup ?? "";
            
                // تعیین ماکزیمم مرحله بر اساس گروه ساختمانی
                var maxStage = feeStages > 0 ? feeStages : GetMaxStageForGroup(buildingGroup);

                // تاریخ صدور پروانه
                var permitDate = c.Specification?.PermitDate ?? "";

                foreach (var stage in Enumerable.Range(1, maxStage))
                {
                    ct.ThrowIfCancellationRequested();
                
                    var reportKey = $"{c.CaseNumber}|{stage}";
                    var hasReport = actualFiles.Contains(reportKey);

                    // ─── مقایسه با اسکن قبلی ───
                    bool isChanged = false;
                    if (!isFirstScan)
                    {
                        if (_previousScanState.TryGetValue(reportKey, out bool prevHasReport))
                        {
                            isChanged = hasReport != prevHasReport;
                        }
                        else
                        {
                            isChanged = true;
                        }
                    }

                    currentScanState[reportKey] = hasReport;

                    // محاسبه مهلت - بر اساس ماه‌های تنظیم شده برای این مرحله و گروه
                    var monthsForStage = GetMonthsForStage(buildingGroup, stage);
                    var deadline = CalculateDeadlineFromMonths(permitDate, monthsForStage);
                    var daysRemaining = CalculateDaysRemaining(deadline);
                    var daysRemainingSort = CalculateDaysRemainingSort(deadline);
                    var deadlineStatus = GetDeadlineStatus(hasReport, daysRemaining);

                    // تاریخ تولید
                    var generatedAt = "";
                    if (hasReport && generatedDates.TryGetValue(reportKey, out var gDate))
                        generatedAt = gDate;

                    var item = new CaseReportItem
                    {
                        CaseNumber = c.CaseNumber,
                        PermitNumber = c.Specification?.PermitNumber ?? "",
                        RowNumber = rowMap.TryGetValue(c.CaseNumber, out var rn) ? rn : 0,
                        Owner = c.Owner,
                        FullAddress = c.Specification?.Address ?? "",
                        Companion = c.OwnerMobile,
                        Stage = stage,
                        StageName = StageReportRules.StageName(stage),
                        Status = hasReport ? "✅ تولید شده" : "⏳ در انتظار",
                        GeneratedAt = generatedAt,
                        GeneratedAtShamsi = PersianDateHelper.GregoryStringToShamsi(generatedAt),
                        PermitDate = NormalizePermitDate(c.Specification?.PermitDate),
                        Deadline = deadline,
                        DaysRemaining = daysRemaining,
                        DaysRemainingSort = daysRemainingSort,
                        DeadlineStatus = deadlineStatus,
                        BuildingGroup = buildingGroup,
                        IsChanged = isChanged,
                    };
                
                    // Add to list on UI thread for thread safety
                    Application.Current.Dispatcher.Invoke(() => _allReportItems.Add(item));
                }
            }

            // ─── آپدیت کش (حافظه + DB) ───
            _previousScanState = currentScanState;
            _db.SaveScanCache(currentScanState);

            // ─── ذخیره نتایج در LastScanResults (فقط آخرین اسکن) ───
            var scanResults = _allReportItems.Select(item => new LastScanResultItem
            {
                CaseNumber = item.CaseNumber,
                Stage = item.Stage,
                Owner = item.Owner,
                FullAddress = item.FullAddress,
                PermitNumber = item.PermitNumber,
                Companion = item.Companion,
                StageName = item.StageName,
                Status = item.Status,
                GeneratedAt = item.GeneratedAt,
                GeneratedAtShamsi = item.GeneratedAtShamsi,
                PermitDate = item.PermitDate,
                Deadline = item.Deadline,
                DaysRemaining = item.DaysRemaining,
                DeadlineStatus = item.DeadlineStatus,
                BuildingGroup = item.BuildingGroup,
                HasReport = item.Status.Contains("تولید شده"),
            }).ToList();
            _db.SaveLastScanResults(scanResults);

            // Final UI updates
            Application.Current.Dispatcher.Invoke(() =>
            {
                Progress = 100;
                ProgressText = "";
                ApplyReportFilters();

                if (isFirstScan)
                    StatusMessage = $"اسکن انجام شد: {_allReportItems.Count} مورد یافت شد";
                else
                {
                    int changedCount = _allReportItems.Count(i => i.IsChanged);
                    StatusMessage = changedCount > 0
                        ? $"اسکن انجام شد: {changedCount} تغییر نسبت به اسکن قبلی"
                        : "اسکن انجام شد: بدون تغییر";
                }
            });
        }

    /// <summary>
    /// اعمال فیلترها روی لیست گزارش‌ها
    /// </summary>
    private void ApplyReportFilters()
    {
        var filtered = _allReportItems.AsEnumerable();

        // فیلتر مرحله: 0=همه, 1=اول, 2=دوم, 3=سوم
        if (FilterStage > 0)
            filtered = filtered.Where(r => r.Stage == FilterStage);

        // فیلتر وضعیت: 0=همه, 1=در انتظار, 2=تولید شده
        if (FilterStatus == 1)
            filtered = filtered.Where(r => r.Status.Contains("در انتظار"));
        else if (FilterStatus == 2)
            filtered = filtered.Where(r => r.Status.Contains("تولید شده"));

        ReportStatus.Clear();
        foreach (var item in filtered)
            ReportStatus.Add(item);

        OnPropertyChanged(nameof(CountDisplay));
    }

    /// <summary>
    /// نرمال‌سازی تاریخ صدور: ارقام فارسی→لاتین، پد ماه/روز، اصلاح ماه 0→1 (داده کثیف مثل 1405/0/02)
    /// </summary>
    private static string NormalizePermitDate(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        var ascii = DigitNormalizer.ToAsciiDigits(input.Trim());
        var parts = ascii.Split('/');
        if (parts.Length != 3) return ascii.Trim();
        if (!int.TryParse(parts[0].Trim(), out int y)) return ascii.Trim();
        if (!int.TryParse(parts[1].Trim(), out int m)) return ascii.Trim();
        if (!int.TryParse(parts[2].Trim(), out int d)) return ascii.Trim();
        if (m < 1) m = 1; if (m > 12) m = 12;
        if (d < 1) d = 1; if (d > 31) d = 31;
        return $"{y}/{m:D2}/{d:D2}";
    }

    private string CalculateDeadline(string permitDate, int stage, int intervalMonths)
    {
        var norm = NormalizePermitDate(permitDate);
        if (string.IsNullOrWhiteSpace(norm)) return "";

        try
        {
            var parts = norm.Split('/');
            if (parts.Length != 3) return "";

            int year = int.Parse(parts[0]);
            int month = int.Parse(parts[1]);
            int day = int.Parse(parts[2]);

            var pc = new PersianCalendar();
            // Add months directly to Persian date using PersianCalendar
            var deadlineDate = pc.ToDateTime(year, month, day, 0, 0, 0, 0);
            deadlineDate = pc.AddMonths(deadlineDate, intervalMonths * stage);

            // Extract Persian date components
            int rY = pc.GetYear(deadlineDate);
            int rM = pc.GetMonth(deadlineDate);
            int rD = pc.GetDayOfMonth(deadlineDate);

            return $"{rY}/{rM:D2}/{rD:D2}";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// محاسبه تعداد روزهای مانده تا مهلت (شمسی)
    /// </summary>
    private string CalculateDaysRemaining(string deadline)
    {
        if (string.IsNullOrWhiteSpace(deadline)) return "";

        try
        {
            var parts = deadline.Split('/');
            if (parts.Length != 3) return "";

            int year = int.Parse(parts[0]);
            int month = int.Parse(parts[1]);
            int day = int.Parse(parts[2]);

            var pc = new PersianCalendar();
            var deadlineGregorian = pc.ToDateTime(year, month, day, 0, 0, 0, 0);
            var today = DateTime.Today;
            var diff = (deadlineGregorian - today).Days;

            if (diff < 0) return $"🔴 {Math.Abs(diff)} روز گذشته";
            if (diff == 0) return "⚠️ امروز";
            if (diff <= 7) return $"⚠️ {diff} روز";
            return $"📅 {diff} روز";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// مقدار عددی مانده برای مرتب‌سازی: منفی=گذشته، ۰=امروز، مثبت=آینده، int.MaxValue=بدون مهلت (آخر جدول)
    /// </summary>
    private int CalculateDaysRemainingSort(string deadline)
    {
        if (string.IsNullOrWhiteSpace(deadline)) return int.MaxValue;
        try
        {
            var parts = deadline.Split('/');
            if (parts.Length != 3) return int.MaxValue;
            int year = int.Parse(parts[0]);
            int month = int.Parse(parts[1]);
            int day = int.Parse(parts[2]);
            var pc = new PersianCalendar();
            var deadlineGregorian = pc.ToDateTime(year, month, day, 0, 0, 0, 0);
            return (deadlineGregorian - DateTime.Today).Days;
        }
        catch { return int.MaxValue; }
    }

    /// <summary>
    /// تعیین وضعیت مهلت
    /// </summary>
    private string GetDeadlineStatus(bool hasReport, string daysRemaining)
    {
        if (hasReport) return "✅";
        if (string.IsNullOrEmpty(daysRemaining)) return "";
        if (daysRemaining.Contains("گذشته") || daysRemaining.Contains("امروز")) return "🔴";
        if (daysRemaining.Contains("روز") && !daysRemaining.Contains("📅"))
        {
            // استخراج عدد روز
            var numStr = new string(daysRemaining.Where(char.IsDigit).ToArray());
            if (int.TryParse(numStr, out int days) && days <= 7) return "⚠️";
        }
        return "📅";
    }

    private int GetRowNumber(string caseNumber)
    {
        var cases = _db.LoadCases(_db.GetActiveSnapshotId());
        var idx = cases.FindIndex(c => c.CaseNumber == caseNumber);
        return idx >= 0 ? idx + 1 : -1; // -1 = پرونده یافت نشد
    }

    private void SelectAll()
    {
        foreach (var item in ReportStatus)
            item.IsSelected = true;
    }

    private void DeselectAll()
    {
        foreach (var item in ReportStatus)
            item.IsSelected = false;
    }

    private void Preview()
    {
        var selected = ReportStatus.Where(r => r.IsSelected && !r.Status.Contains("تولید شده")).ToList();
        if (selected.Count == 0)
        {
            PreviewText = "موردی انتخاب نشده";
            return;
        }

        var ltrSuffix = UseLtr ? " - LTR" : "";
        var lines = new List<string> { $"گزارش‌های آماده تولید ({selected.Count} مورد):", "" };

        foreach (var item in selected)
        {
            var folderName = StageReportRules.GenerateFolderName(item.RowNumber, new Case { CaseNumber = item.CaseNumber, Owner = item.Owner });
            lines.Add($"- {item.Owner} ({item.StageName}), {item.CaseNumber} -> {folderName}{ltrSuffix}/گزارش مرحله {item.Stage}{ltrSuffix}.docx");
        }

        PreviewText = string.Join("\r\n", lines);
    }

    /// <summary>
    /// تولید گزارش‌های انتخاب شده
    /// </summary>
    private void Generate()
    {
        try
        {
            Directory.CreateDirectory(_outputPath);
            var cases = _db.LoadCases(_db.GetActiveSnapshotId());
            var generator = CreateGenerator();

            int generated = 0, failed = 0, skipped = 0;
            var selected = ReportStatus.Where(r => r.IsSelected).ToList();
            int total = selected.Count;

            if (total == 0)
            {
                StatusMessage = "هیچ موردی انتخاب نشده";
                return;
            }

            int current = 0;
            foreach (var item in selected)
            {
                current++;
                Progress = (int)((double)current / total * 100);
                ProgressText = $"{current}/{total}";

                var c = cases.FirstOrDefault(x => x.CaseNumber == item.CaseNumber);
                if (c == null) { failed++; item.Status = "❌ پرونده یافت نشد"; continue; }

                var rowNumber = GetRowNumber(c.CaseNumber);
                if (rowNumber <= 0)
                {
                    failed++;
                    item.Status = $"❌ پرونده {c.CaseNumber} در لیست پرونده‌ها یافت نشد (ردیف نامعتبر)";
                    continue;
                }

                var templateForStage = generator.FindTemplateForStage(item.Stage);
                if (templateForStage == null)
                {
                    failed++;
                    item.Status = $"❌ تمپلیت مرحله {item.StageName} یافت نشد";
                    continue;
                }

                var genResult = generator.GenerateReport(c, item.Stage, rowNumber, _outputPath, DateTime.Now, templateForStage, UseLtr);

                if (genResult.Success && genResult.OutputPath != null && File.Exists(genResult.OutputPath) && new FileInfo(genResult.OutputPath).Length > 0)
                {
                    var numFormat = UseLtr ? "LTR" : "Persian";
                    var fileHash = NezamDatabase.ComputeFileHash(genResult.OutputPath);
                    _db.SaveGeneratedReport(c.CaseNumber, item.Stage, Path.GetFileNameWithoutExtension(templateForStage), genResult.OutputPath, c.Owner, numFormat, fileHash);
                    generated++;
                    item.Status = "✅ تولید شده";
                    var now2 = DateTime.Now;
                    item.GeneratedAt = now2.ToString("yyyy/MM/dd HH:mm");
                    item.GeneratedAtShamsi = PersianDateHelper.ToShamsiDateTime(now2);
                    item.IsSelected = false;
                }
                else if (genResult.Success)
                {
                    failed++;
                    item.Status = "❌ فایل ایجاد نشد";
                }
                else
                {
                    failed++;
                    item.Status = $"❌ {genResult.ErrorMessage}";
                }
            }

            StatusMessage = $"تکمیل: {generated} تولید شد | {failed} خطا | {skipped} لغو شد | مسیر: {_outputPath}";
            Progress = 100;
            LoadHistory();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطا: {ex.Message}";
        }
    }

    /// <summary>
    /// بارگذاری تاریخچه گزارش‌های تولید شده
    /// </summary>
    public void LoadHistory()
    {
        try
        {
            ReportHistory.Clear();
            var reports = _db.GetGeneratedReports();
            int row = 1;
            foreach (var r in reports)
            {
                if (HistoryFilterStage > 0 && r.Stage != HistoryFilterStage) continue;
                if (HistoryFilterFormat > 0)
                {
                    var fmtFilter = HistoryFilterFormat == 1 ? "Persian" : (HistoryFilterFormat == 2 ? "LTR" : "English");
                    if (r.NumberFormat != fmtFilter) continue;
                }
                if (!string.IsNullOrEmpty(HistorySearchText))
                {
                    var normSearch = FilterHelper.Normalize(HistorySearchText);
                    var normStored = FilterHelper.Normalize($"{r.OwnerName} {r.CaseNumber} {r.NumberFormat}");
                    if (!normStored.Contains(normSearch, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                var fileExists = !string.IsNullOrEmpty(r.OutputPath) && File.Exists(r.OutputPath);
                var folderPath = Path.GetDirectoryName(r.OutputPath) ?? "";
                var displayTime = FormatTimestamp(r.CreatedAt);

                string status;
                if (!fileExists)
                    status = "⚠️ فایل موجود نیست";
                else if (!string.IsNullOrEmpty(r.FileHash))
                {
                    var currentHash = NezamDatabase.ComputeFileHash(r.OutputPath);
                    status = currentHash == r.FileHash ? "✅ موجود" : "🔄 فایل تغییر کرده (بازنویسی شده)";
                }
                else
                    status = "✅ موجود";

                ReportHistory.Add(new ReportHistoryItem
                {
                    RowId = row++,
                    CaseNumber = r.CaseNumber,
                    OwnerName = r.OwnerName,
                    Owner = r.OwnerName,
                    Stage = r.Stage,
                    StageName = StageReportRules.StageName(r.Stage),
                    ReportType = StageReportRules.StageName(r.Stage),
                    ReportDate = displayTime,
                    NumberFormat = r.NumberFormat,
                    OutputPath = r.OutputPath,
                    Status = status,
                    FileExists = fileExists,
                    FolderPath = folderPath
                });
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطا در بارگذاری تاریخچه: {ex.Message}";
        }
    }

    private string FormatTimestamp(string? createdAt)
    {
        if (string.IsNullOrEmpty(createdAt)) return "نامشخص";
        if (createdAt.Contains('-') && createdAt.Length >= 10)
            return createdAt.Split('T')[0].Replace('-', '/');
        return createdAt;
    }

    public ObservableCollection<ReportHistoryItem> ReportHistory { get; } = new();

    /// <summary>
    /// باز کردن فایل گزارش
    /// </summary>
    public void OpenReport(ReportHistoryItem? item)
    {
        if (item == null || string.IsNullOrEmpty(item.OutputPath) || !File.Exists(item.OutputPath))
        {
            StatusMessage = "فایل یافت نشد";
            return;
        }
        Process.Start(new ProcessStartInfo
        {
            FileName = item.OutputPath,
            UseShellExecute = true
        });
        StatusMessage = $"باز شد: {item.FileName}";
    }

    /// <summary>
    /// باز کردن پوشه گزارش
    /// </summary>
    public void OpenFolder(ReportHistoryItem? item)
    {
        if (item == null)
        {
            StatusMessage = "موردی انتخاب نشده";
            return;
        }

        var folderPath = item.FolderPath;
        if (string.IsNullOrEmpty(folderPath) && !string.IsNullOrEmpty(item.OutputPath))
        {
            folderPath = Path.GetDirectoryName(item.OutputPath);
        }

        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            StatusMessage = $"پوشه یافت نشد: {folderPath ?? "نامشخص"}";
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = folderPath,
            UseShellExecute = true
        });
        StatusMessage = $"باز شد: {folderPath}";
    }

    /// <summary>
    /// باز کردن پوشه پرونده (از طریق context menu میز کار)
    /// </summary>
    public void OpenFolderForCase(CaseReportItem item)
    {
        if (item == null) return;

        // جستجوی پوشه بر اساس شماره پرونده در outputs/
        if (Directory.Exists(_outputPath))
        {
            // CaseNumber مثل "1404/514" → در نام پوشه "514-1404"
            var caseParts = item.CaseNumber.Split('/');
            var searchPattern = caseParts.Length == 2 ? $"{caseParts[1]}-{caseParts[0]}" : item.CaseNumber;

            foreach (var dir in Directory.GetDirectories(_outputPath))
            {
                var dirName = Path.GetFileName(dir);
                if (dirName.Contains(searchPattern))
                {
                    Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
                    StatusMessage = $"باز شد: {dir}";
                    return;
                }
            }
        }

        StatusMessage = $"پوشه‌ای برای پرونده {item.CaseNumber} یافت نشد";
    }

    /// <summary>
    /// حذف فایل و لاگ با تایید کاربر
    /// </summary>
    public void DeleteReportWithConfirmation(ReportHistoryItem? item)
    {
        if (item == null) return;
        if (!item.FileExists && string.IsNullOrEmpty(item.OutputPath)) return;

        var confirmMsg = $"آیا از حذف فایل و لاگ این گزارش اطمینان دارید?\n\nمالک: {item.OwnerName}\nمرحله: {item.StageName}\nفایل: {item.FileName}\n\nاین عملیات غیرقابل بازگشت است.";
        var confirm = MessageBox.Show(confirmMsg, "تایید حذف", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            StatusMessage = "لغو حذف";
            return;
        }

        try
        {
            if (item.FileExists && !string.IsNullOrEmpty(item.OutputPath))
                File.Delete(item.OutputPath);

            _db.DeleteGeneratedReportByPath(item.OutputPath);

            if (!string.IsNullOrEmpty(item.FolderPath) && Directory.Exists(item.FolderPath))
            {
                if (!Directory.EnumerateFileSystemEntries(item.FolderPath).Any())
                    Directory.Delete(item.FolderPath);
                else
                    StatusMessage = $"فایل و لاگ حذف شد: {item.FileName} (پوشه خالی نیست)";
            }
            else
            {
                StatusMessage = $"فایل و لاگ حذف شد: {item.FileName}";
            }
            LoadHistory();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطا در حذف: {ex.Message}";
        }
    }

    /// <summary>
    /// حذف فقط لاگ (بدون حذف فایل)
    /// </summary>
    public void DeleteLog(ReportHistoryItem? item)
    {
        if (item == null) { StatusMessage = "موردی انتخاب نشده"; return; }
        try
        {
            _db.DeleteGeneratedReportByPath(item.OutputPath);
            StatusMessage = $"لاگ حذف شد: {item.FileName} (فایل روی دیسک حذف نشد)";
            LoadHistory();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطا در حذف لاگ: {ex.Message}";
        }
    }

    /// <summary>
    /// حذف لاگ‌های انتخاب‌شده (بدون حذف فایل) — پشتیبانی از انتخاب چند ردیفی
    /// </summary>
    public void DeleteSelectedHistory(List<ReportHistoryItem> items)
    {
        if (items == null || items.Count == 0) { StatusMessage = "موردی انتخاب نشده"; return; }

        var confirmMsg = items.Count == 1
            ? $"آیا از حذف لاگ این گزارش اطمینان دارید?\n\nمالک: {items[0].OwnerName}\nمرحله: {items[0].StageName}\nفایل: {items[0].FileName}"
            : $"آیا از حذف {items.Count} لاگ انتخاب‌شده اطمینان دارید?\n\nفایل‌ها روی دیسک حذف نخواهند شد.";

        var confirm = MessageBox.Show(confirmMsg, "تایید حذف لاگ", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) { StatusMessage = "لغو حذف"; return; }

        int deleted = 0;
        foreach (var item in items)
        {
            try
            {
                _db.DeleteGeneratedReportByPath(item.OutputPath);
                deleted++;
            }
            catch { }
        }
        StatusMessage = $"{deleted} لاگ حذف شد (فایل‌ها روی دیسک حذف نشدند)";
        LoadHistory();
    }

    /// <summary>
    /// حذف فایل و لاگ‌های انتخاب‌شده — پشتیبانی از انتخاب چند ردیفی
    /// </summary>
    public void DeleteSelectedHistoryWithFile(List<ReportHistoryItem> items)
    {
        if (items == null || items.Count == 0) { StatusMessage = "موردی انتخاب نشده"; return; }

        var fileList = string.Join("\n", items.Take(10).Select(i => $"• {i.OwnerName} — {i.StageName}"));
        if (items.Count > 10) fileList += $"\n... و {items.Count - 10} مورد دیگر";

        var confirmMsg = items.Count == 1
            ? $"آیا از حذف فایل و لاگ این گزارش اطمینان دارید?\n\nمالک: {items[0].OwnerName}\nمرحله: {items[0].StageName}\nفایل: {items[0].FileName}\n\nاین عملیات غیرقابل بازگشت است."
            : $"آیا از حذف {items.Count} فایل و لاگ انتخاب‌شده اطمینان دارید?\n\n{fileList}\n\nاین عملیات غیرقابل بازگشت است.";

        var confirm = MessageBox.Show(confirmMsg, "تایید حذف فایل و لاگ", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) { StatusMessage = "لغو حذف"; return; }

        int deleted = 0, errors = 0;
        foreach (var item in items)
        {
            try
            {
                // حذف فایل از دیسک
                if (item.FileExists && !string.IsNullOrEmpty(item.OutputPath) && File.Exists(item.OutputPath))
                    File.Delete(item.OutputPath);

                // حذف لاگ از دیتابیس
                _db.DeleteGeneratedReportByPath(item.OutputPath);
                deleted++;

                // حذف پوشه خالی
                if (!string.IsNullOrEmpty(item.FolderPath) && Directory.Exists(item.FolderPath))
                {
                    if (!Directory.EnumerateFileSystemEntries(item.FolderPath).Any())
                        Directory.Delete(item.FolderPath);
                }
            }
            catch { errors++; }
        }

        StatusMessage = errors == 0
            ? $"{deleted} فایل و لاگ حذف شد"
            : $"{deleted} حذف شد، {errors} خطا";
        LoadHistory();
    }

    /// <summary>
    /// تولید مجدد با تایید کاربر
    /// </summary>
    public void RegenerateWithConfirmation(ReportHistoryItem? item)
    {
        if (item == null) { StatusMessage = "موردی انتخاب نشده"; return; }

        var confirmMsg = $"این گزارش قبلاً تولید شده است.\n\nآیا می‌خواهید آن را مجدداً تولید کنید?\n\nمالک: {item.OwnerName}\nمرحله: {item.StageName}";
        var confirm = MessageBox.Show(confirmMsg, "تولید مجدد", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
        {
            StatusMessage = "لغو تولید مجدد";
            return;
        }

        var cases = _db.LoadCases(_db.GetActiveSnapshotId());
        var c = cases.FirstOrDefault(x => x.CaseNumber == item.CaseNumber);
        if (c == null) { StatusMessage = "پرونده یافت نشد"; return; }

        var idx = cases.IndexOf(c);
        var isLtr = item.NumberFormat == "LTR";
        var generator = CreateGenerator(Path.GetDirectoryName(SelectedTemplatePath) ?? _templatePath);
        var genResult = generator.GenerateReport(c, item.Stage, idx + 1, _outputPath, DateTime.Now, null, isLtr);

        if (genResult.Success && genResult.OutputPath != null && File.Exists(genResult.OutputPath))
        {
            _db.DeleteGeneratedReport(c.CaseNumber, item.Stage);
            var numFormat = isLtr ? "LTR" : "Persian";
            var fileHash = NezamDatabase.ComputeFileHash(genResult.OutputPath);
            _db.SaveGeneratedReport(c.CaseNumber, item.Stage, SelectedTemplateName, genResult.OutputPath, c.Owner, numFormat, fileHash);
            StatusMessage = $"تولید مجدد: {item.CaseNumber} مرحله {item.StageName} ✅";
            LoadHistory();
        }
        else
        {
            StatusMessage = $"خطا: {genResult.ErrorMessage}";
        }
    }

    public void SyncOutputFolder()
        {
            try
            {
                var result = _db.SyncOutputFolder(_outputPath);
                StatusMessage = $"sync: added={result.added}, removed={result.removed}, existing={result.existing}";
                LoadHistory();
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطا در سینک: {ex.Message}";
            }
        }

        /// <summary>
        /// کلاس تنظیمات هر مرحله گزارش
        /// </summary>
        public class StageConfiguration : INotifyPropertyChanged
        {
            private int _configuredMonths;
            public int StageNumber { get; set; }
            public string StageTitle { get; set; } = "";
            public int DefaultMonths { get; set; }
            public string DeadlineType { get; set; } = "";
            public string BuildingGroup { get; set; } = "";
            public string Discipline { get; set; } = "";
            public Action<int>? OnConfiguredMonthsChanged { get; set; }

            public int ConfiguredMonths
                        {
                            get => _configuredMonths;
                            set
                            {
                                if (_configuredMonths != value && value >= 1 && value <= 30)
                                {
                                    _configuredMonths = value;
                                    OnPropertyChanged();
                                    OnConfiguredMonthsChanged?.Invoke(value);
                                }
                            }
                        }

            public event PropertyChangedEventHandler? PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string? name = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>
        /// بروزرسانی لیست مراحل بر اساس گروه و رشته انتخاب شده
        /// </summary>
        private void UpdateStageConfigurations()
        {
            StageConfigurations.Clear();

            var configs = GetStageConfigurations(SelectedBuildingGroup, SelectedEngineeringDiscipline);
            foreach (var config in configs)
            {
                StageConfigurations.Add(config);
            }
        }

        /// <summary>
        /// دریافت تنظیمات مراحل برای گروه و رشته مشخص
        /// </summary>
        private List<StageConfiguration> GetStageConfigurations(string buildingGroup, string discipline)
        {
            var configs = new List<StageConfiguration>();

            switch (buildingGroup)
            {
                case "الف":
                    // گروه الف: ۳ مرحله
                    configs.Add(new StageConfiguration
                    {
                        StageNumber = 1,
                        StageTitle = "گزارش مربوط به سقف آخر طبق پروانه",
                        DefaultMonths = _groupAMonths[1],
                        ConfiguredMonths = _groupAMonths[1],
                        DeadlineType = "نوبت پرداخت",
                        BuildingGroup = buildingGroup,
                        Discipline = discipline,
                        OnConfiguredMonthsChanged = (value) =>
                        {
                            _groupAMonths[1] = value;
                            _db.SaveSetting("report_group_a_stage_1_months", value.ToString());
                            ApplyReportFilters();
                        }
                    });
                    configs.Add(new StageConfiguration
                    {
                        StageNumber = 2,
                        StageTitle = GetFinalReportTitle(discipline, "گزارش نهایی"),
                        DefaultMonths = _groupAMonths[2],
                        ConfiguredMonths = _groupAMonths[2],
                        DeadlineType = "نوبت پرداخت",
                        BuildingGroup = buildingGroup,
                        Discipline = discipline,
                        OnConfiguredMonthsChanged = (value) =>
                        {
                            _groupAMonths[2] = value;
                            _db.SaveSetting("report_group_a_stage_2_months", value.ToString());
                            ApplyReportFilters();
                        }
                    });
                    configs.Add(new StageConfiguration
                    {
                        StageNumber = 3,
                        StageTitle = "گزارش نهایی پروژه",
                        DefaultMonths = _groupAMonths[3],
                        ConfiguredMonths = _groupAMonths[3],
                        DeadlineType = "شرط گزارش",
                        BuildingGroup = buildingGroup,
                        Discipline = discipline,
                        OnConfiguredMonthsChanged = (value) =>
                        {
                            _groupAMonths[3] = value;
                            _db.SaveSetting("report_group_a_stage_3_months", value.ToString());
                            ApplyReportFilters();
                        }
                    });
                    break;

                case "ب":
                    // گروه ب: ۴ مرحله
                    configs.Add(new StageConfiguration
                    {
                        StageNumber = 1,
                        StageTitle = "گزارش سقف دوم",
                        DefaultMonths = _groupBMonths[1],
                        ConfiguredMonths = _groupBMonths[1],
                        DeadlineType = "نوبت پرداخت",
                        BuildingGroup = buildingGroup,
                        Discipline = discipline,
                        OnConfiguredMonthsChanged = (value) =>
                        {
                            _groupBMonths[1] = value;
                            _db.SaveSetting("report_group_b_stage_1_months", value.ToString());
                            ApplyReportFilters();
                        }
                    });
                    configs.Add(new StageConfiguration
                    {
                        StageNumber = 2,
                        StageTitle = "گزارش سقف آخر",
                        DefaultMonths = _groupBMonths[2],
                        ConfiguredMonths = _groupBMonths[2],
                        DeadlineType = "نوبت پرداخت",
                        BuildingGroup = buildingGroup,
                        Discipline = discipline,
                        OnConfiguredMonthsChanged = (value) =>
                        {
                            _groupBMonths[2] = value;
                            _db.SaveSetting("report_group_b_stage_2_months", value.ToString());
                            ApplyReportFilters();
                        }
                    });
                    configs.Add(new StageConfiguration
                    {
                        StageNumber = 3,
                        StageTitle = GetFinalReportTitle(discipline, "گزارش نهایی"),
                        DefaultMonths = _groupBMonths[3],
                        ConfiguredMonths = _groupBMonths[3],
                        DeadlineType = "نوبت پرداخت",
                        BuildingGroup = buildingGroup,
                        Discipline = discipline,
                        OnConfiguredMonthsChanged = (value) =>
                        {
                            _groupBMonths[3] = value;
                            _db.SaveSetting("report_group_b_stage_3_months", value.ToString());
                            ApplyReportFilters();
                        }
                    });
                    configs.Add(new StageConfiguration
                    {
                        StageNumber = 4,
                        StageTitle = GetFinalReportDetailTitle(discipline),
                        DefaultMonths = _groupBMonths[4],
                        ConfiguredMonths = _groupBMonths[4],
                        DeadlineType = "شرط گزارش",
                        BuildingGroup = buildingGroup,
                        Discipline = discipline,
                        OnConfiguredMonthsChanged = (value) =>
                        {
                            _groupBMonths[4] = value;
                            _db.SaveSetting("report_group_b_stage_4_months", value.ToString());
                            ApplyReportFilters();
                        }
                    });
                    break;

                case "ج":
                case "د":
                    // گروه ج و د: ۴ مرحله
                    var monthsArray = buildingGroup == "ج" ? _groupCMonths : _groupDMonths;
                    var prefix = buildingGroup == "ج" ? "c" : "d";

                    configs.Add(new StageConfiguration
                    {
                        StageNumber = 1,
                        StageTitle = "گزارش سقف سوم",
                        DefaultMonths = monthsArray[1],
                        ConfiguredMonths = monthsArray[1],
                        DeadlineType = "نوبت پرداخت",
                        BuildingGroup = buildingGroup,
                        Discipline = discipline,
                        OnConfiguredMonthsChanged = (value) =>
                        {
                            monthsArray[1] = value;
                            _db.SaveSetting($"report_group_{prefix}_stage_1_months", value.ToString());
                            ApplyReportFilters();
                        }
                    });
                    configs.Add(new StageConfiguration
                    {
                        StageNumber = 2,
                        StageTitle = "گزارش سقف آخر",
                        DefaultMonths = monthsArray[2],
                        ConfiguredMonths = monthsArray[2],
                        DeadlineType = "نوبت پرداخت",
                        BuildingGroup = buildingGroup,
                        Discipline = discipline,
                        OnConfiguredMonthsChanged = (value) =>
                        {
                            monthsArray[2] = value;
                            _db.SaveSetting($"report_group_{prefix}_stage_2_months", value.ToString());
                            ApplyReportFilters();
                        }
                    });
                    configs.Add(new StageConfiguration
                    {
                        StageNumber = 3,
                        StageTitle = GetFinalReportTitle(discipline, "گزارش نهایی"),
                        DefaultMonths = monthsArray[3],
                        ConfiguredMonths = monthsArray[3],
                        DeadlineType = "نوبت پرداخت",
                        BuildingGroup = buildingGroup,
                        Discipline = discipline,
                        OnConfiguredMonthsChanged = (value) =>
                        {
                            monthsArray[3] = value;
                            _db.SaveSetting($"report_group_{prefix}_stage_3_months", value.ToString());
                            ApplyReportFilters();
                        }
                    });
                    configs.Add(new StageConfiguration
                    {
                        StageNumber = 4,
                        StageTitle = GetFinalReportDetailTitle(discipline),
                        DefaultMonths = monthsArray[4],
                        ConfiguredMonths = monthsArray[4],
                        DeadlineType = "شرط گزارش",
                        BuildingGroup = buildingGroup,
                        Discipline = discipline,
                        OnConfiguredMonthsChanged = (value) =>
                        {
                            monthsArray[4] = value;
                            _db.SaveSetting($"report_group_{prefix}_stage_4_months", value.ToString());
                            ApplyReportFilters();
                        }
                    });
                    break;
            }

            return configs;
        }

        /// <summary>
        /// دریافت عنوان گزارش نهایی بر اساس رشته مهندسی
        /// </summary>
        private string GetFinalReportTitle(string discipline, string baseTitle)
        {
            return discipline switch
            {
                "عمران" => "گزارش پایان نازک‌کاری (عمران)",
                "معماری" => "گزارش پایان نازک‌کاری (معماری)",
                "برق" => "گزارش پایان عملیات برقی",
                "مکانیک" => "گزارش اتمام تأسیسات مکانیکی",
                "نقشه‌برداری" => $"{baseTitle} (نقشه‌برداری)",
                "شهرسازی" => $"{baseTitle} (شهرسازی)",
                _ => baseTitle
            };
        }

        /// <summary>
        /// دریافت عنوان تفصیلی گزارش نهایی برای مرحله ۴
        /// </summary>
        private string GetFinalReportDetailTitle(string discipline)
        {
            return discipline switch
            {
                "عمران" => "آزادسازی حسن انجام کار (عمران)",
                "معماری" => "آزادسازی حسن انجام کار (معماری)",
                "برق" => "آزادسازی حسن انجام کار (برق)",
                "مکانیک" => "آزادسازی حسن انجام کار (مکانیک)",
                "نقشه‌برداری" => "گزارش نهایی نقشه‌برداری",
                "شهرسازی" => "گزارش نهایی شهرسازی",
                _ => "آزادسازی حسن انجام کار"
            };
        }

        /// <summary>
        /// محاسبه مهلت بر اساس ماه‌های تنظیم شده برای یک مرحله خاص
        /// </summary>
        private string CalculateDeadlineFromMonths(string permitDate, int monthsToAdd)
        {
            var norm = NormalizePermitDate(permitDate);
            if (string.IsNullOrWhiteSpace(norm)) return "";

            try
            {
                var parts = norm.Split('/');
                if (parts.Length != 3) return "";

                int year = int.Parse(parts[0]);
                int month = int.Parse(parts[1]);
                int day = int.Parse(parts[2]);

                var pc = new PersianCalendar();
                // Add months directly to Persian date using PersianCalendar
                var deadlineDate = pc.ToDateTime(year, month, day, 0, 0, 0, 0);
                deadlineDate = pc.AddMonths(deadlineDate, monthsToAdd);

                // Extract Persian date components
                int rY = pc.GetYear(deadlineDate);
                int rM = pc.GetMonth(deadlineDate);
                int rD = pc.GetDayOfMonth(deadlineDate);

                return $"{rY}/{rM:D2}/{rD:D2}";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// بازگردانی مقادیر پیش‌فرض
        /// </summary>
        private void ResetSchedulingDefaults()
        {
            // Reset Group A defaults
            _groupAMonths[1] = 2; _groupAMonths[2] = 6; _groupAMonths[3] = 10;
            _db.SaveSetting("report_group_a_stage_1_months", "2");
            _db.SaveSetting("report_group_a_stage_2_months", "6");
            _db.SaveSetting("report_group_a_stage_3_months", "10");

            // Reset Group B defaults
            _groupBMonths[1] = 3; _groupBMonths[2] = 6; _groupBMonths[3] = 12; _groupBMonths[4] = 20;
            _db.SaveSetting("report_group_b_stage_1_months", "3");
            _db.SaveSetting("report_group_b_stage_2_months", "6");
            _db.SaveSetting("report_group_b_stage_3_months", "12");
            _db.SaveSetting("report_group_b_stage_4_months", "20");

            // Reset Group C defaults
            _groupCMonths[1] = 3; _groupCMonths[2] = 8; _groupCMonths[3] = 16; _groupCMonths[4] = 24;
            _db.SaveSetting("report_group_c_stage_1_months", "3");
            _db.SaveSetting("report_group_c_stage_2_months", "8");
            _db.SaveSetting("report_group_c_stage_3_months", "16");
            _db.SaveSetting("report_group_c_stage_4_months", "24");

            // Reset Group D defaults
            _groupDMonths[1] = 3; _groupDMonths[2] = 8; _groupDMonths[3] = 16; _groupDMonths[4] = 30;
            _db.SaveSetting("report_group_d_stage_1_months", "3");
            _db.SaveSetting("report_group_d_stage_2_months", "8");
            _db.SaveSetting("report_group_d_stage_3_months", "16");
            _db.SaveSetting("report_group_d_stage_4_months", "30");

            // Notify property changes for direct binding properties
            OnPropertyChanged(nameof(GroupAStage1Months));
            OnPropertyChanged(nameof(GroupAStage2Months));
            OnPropertyChanged(nameof(GroupAStage3Months));
            OnPropertyChanged(nameof(GroupBStage1Months));
            OnPropertyChanged(nameof(GroupBStage2Months));
            OnPropertyChanged(nameof(GroupBStage3Months));
            OnPropertyChanged(nameof(GroupBStage4Months));
            OnPropertyChanged(nameof(GroupCStage1Months));
            OnPropertyChanged(nameof(GroupCStage2Months));
            OnPropertyChanged(nameof(GroupCStage3Months));
            OnPropertyChanged(nameof(GroupCStage4Months));
            OnPropertyChanged(nameof(GroupDStage1Months));
            OnPropertyChanged(nameof(GroupDStage2Months));
            OnPropertyChanged(nameof(GroupDStage3Months));
            OnPropertyChanged(nameof(GroupDStage4Months));

            // Update UI
            UpdateStageConfigurations();
            ApplyReportFilters();
            StatusMessage = "مقادیر پیش‌فرض بازیابی شد";
        }

        /// <summary>
                /// ذخیره تنظیمات مرحله (برای فراخوانی از Command)
                /// </summary>
                private void SaveStageConfiguration(int stageNumber)
                {
                    // Settings are already saved via OnConfiguredMonthsChanged callbacks
                    // This method is kept for potential future use or explicit save
                    StatusMessage = $"تنظیمات مرحله {stageNumber} ذخیره شد";
                }

                /// <summary>
                                /// دریافت ماکزیمم تعداد مراحل برای یک گروه ساختمانی
                                /// </summary>
                                private int GetMaxStageForGroup(string buildingGroup)
                                {
                                    if (string.IsNullOrEmpty(buildingGroup)) return 3;
                                    if (buildingGroup.StartsWith("الف")) return 3;
                                    if (buildingGroup.StartsWith("ب")) return 4;
                                    if (buildingGroup.StartsWith("ج")) return 4;
                                    if (buildingGroup.StartsWith("د")) return 4;
                                    return 3;
                                }

                                /// <summary>
                                /// دریافت ماه‌های تنظیم شده برای یک مرحله خاص بر اساس گروه ساختمانی
                                /// </summary>
                                private int GetMonthsForStage(string buildingGroup, int stage)
                                {
                                    if (string.IsNullOrEmpty(buildingGroup)) return 3;
                                    if (buildingGroup.StartsWith("الف")) return _groupAMonths[stage];
                                    if (buildingGroup.StartsWith("ب")) return _groupBMonths[stage];
                                    if (buildingGroup.StartsWith("ج")) return _groupCMonths[stage];
                                    if (buildingGroup.StartsWith("د")) return _groupDMonths[stage];
                                    return 3;
                                }
            }

/// <summary>
/// آیتم وضعیت تولید گزارش (در جدول اسکن)
/// </summary>
public class CaseReportItem : INotifyPropertyChanged
{
    private bool _isSelected;
    private string _status = "";
    private string _generatedAt = "";
    private bool _isChanged;

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public string CaseNumber { get; set; } = "";
    public string PermitNumber { get; set; } = "";
    public int RowNumber { get; set; }
    public string Owner { get; set; } = "";
    public string FullAddress { get; set; } = "";
    public string Companion { get; set; } = "";
    public int Stage { get; set; }
    public string StageName { get; set; } = "";

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public string GeneratedAt
    {
        get => _generatedAt;
        set { _generatedAt = value; OnPropertyChanged(); }
    }
    public string GeneratedAtShamsi { get; set; } = "";

    public bool IsChanged
    {
        get => _isChanged;
        set { _isChanged = value; OnPropertyChanged(); }
    }

    public string PermitDate { get; set; } = "";
    public string Deadline { get; set; } = "";
    public string DaysRemaining { get; set; } = "";
    /// <summary>مقدار عددی برای مرتب‌سازی ستون «مانده»: منفی=گذشته، ۰=امروز، مثبت=آینده، int.MaxValue=بدون مهلت</summary>
    public int DaysRemainingSort { get; set; } = int.MaxValue;
    public string DeadlineStatus { get; set; } = "";
    public string BuildingGroup { get; set; } = "";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// آیتم تاریخچه گزارش‌های تولید شده
/// </summary>
public class ReportHistoryItem : INotifyPropertyChanged
{
    public int RowId { get; set; }
    public string CaseNumber { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public string Owner { get; set; } = "";
    public int Stage { get; set; }
    public string StageName { get; set; } = "";
    public string ReportType { get; set; } = "";
    public string ReportDate { get; set; } = "";
    public string NumberFormat { get; set; } = "";
    public string OutputPath { get; set; } = "";
    public string FileName => Path.GetFileName(OutputPath) ?? "";
    public string Status { get; set; } = "";
    public bool FileExists { get; set; }
    public string FolderPath { get; set; } = "";
    public string FolderName { get; set; } = "";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
