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
    private string _statusMessage = "برای شروع، «اسکن پرونده‌ها» را بزنید";
    private int _progress;
    private string _progressText = "";
    private int _selectedStage = 1;
    private string _selectedTemplateName = "";
    private string _selectedTemplatePath = "";
    private string _reportDate = "";
    private string _historySearchText = "";
    private int _historyFilterStage = 0;
    private int _historyFilterFormat = 0;
    private string _previewText = "";
    private bool _useLtr = false;
    private bool _useSignedTemplates = false;
    private string _signedTemplatePath = "";
    private bool _generatePdf = false;
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

    private bool _isScheduleExpanded = true;
    /// <summary>Schedule-settings card open or collapsed (persisted).</summary>
    public bool IsScheduleExpanded
    {
        get => _isScheduleExpanded;
        set
        {
            if (SetProperty(ref _isScheduleExpanded, value))
            {
                _db.SaveSetting(SettingKeys.ScheduleExpanded, value ? "1" : "0");
                OnPropertyChanged(nameof(ScheduleToggleIcon));
            }
        }
    }
    /// <summary>Collapse toggle glyph: ▼ open, ▶ collapsed.</summary>
    public string ScheduleToggleIcon => IsScheduleExpanded ? "▼" : "▶";
    public ICommand ToggleScheduleCommand { get; }

    private bool _isProductionExpanded;
    /// <summary>
    /// Production-settings card open or collapsed (persisted as
    /// production_expanded; starts hidden until the user opens it once).
    /// </summary>
    public bool IsProductionExpanded
    {
        get => _isProductionExpanded;
        set
        {
            if (SetProperty(ref _isProductionExpanded, value))
            {
                _db.SaveSetting(SettingKeys.ProductionExpanded, value ? "1" : "0");
                OnPropertyChanged(nameof(ProductionToggleIcon));
            }
        }
    }
    /// <summary>Collapse toggle glyph: ▼ open, ▶ collapsed.</summary>
    public string ProductionToggleIcon => IsProductionExpanded ? "▼" : "▶";
    /// <summary>One-line summary shown under the card title (PDF on/off, signed template on/off).</summary>
    public string ProductionSummary => $"PDF: {(GeneratePdf ? "روشن" : "خاموش")}، تمپلیت امضا: {(UseSignedTemplates ? "روشن" : "خاموش")}";
    public ICommand ToggleProductionCommand { get; }
    public ICommand DownloadTemplatesCommand { get; }

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
    /// <summary>تولید با تمپلیت امضاشده (P1) — در تنظیمات ذخیره می‌شود.</summary>
    public bool UseSignedTemplates { get => _useSignedTemplates; set { if (SetProperty(ref _useSignedTemplates, value)) { _db.SaveSetting(SettingKeys.SignedReport, value ? "1" : "0"); OnPropertyChanged(nameof(ProductionSummary)); } } }
    public string SignedTemplatePath { get => _signedTemplatePath; set { if (SetProperty(ref _signedTemplatePath, value)) _db.SaveSetting(SettingKeys.SignedTemplatesPath, value); } }
    /// <summary>همراه هر گزارش Word یک PDF هم بسازد (P2) — نیاز به Word نصب‌شده دارد.</summary>
    public bool GeneratePdf { get => _generatePdf; set { if (SetProperty(ref _generatePdf, value)) { _db.SaveSetting(SettingKeys.GeneratePdf, value ? "1" : "0"); OnPropertyChanged(nameof(ProductionSummary)); } } }
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
            private long _loadedScanSnapshotId = -1;
            private CancellationTokenSource? _scanCts;
            private CancellationTokenSource? _generateCts;

            // زمان‌بندی - آرایه ماه‌ها برای هر گروه و هر مرحله (اندیس ۱-۴ استفاده می‌شود)
        private readonly int[] _groupAMonths = new int[5]; // الف: ۳ مرحله
        private readonly int[] _groupBMonths = new int[5]; // ب: ۴ مرحله
        private readonly int[] _groupCMonths = new int[5]; // ج: ۴ مرحله
        private readonly int[] _groupDMonths = new int[5]; // د: ۴ مرحله

        // Properties for each stage's months per group (for direct binding if needed).
        // All 16 delegate to one helper so the setting key and the refresh
        // behavior stay identical for every group/stage.
        private bool SetGroupMonths(int[] months, int stage, string settingKey, int value, [CallerMemberName] string? propertyName = null)
        {
            if (months[stage] == value) return false;
            months[stage] = value;
            _db.SaveSetting(settingKey, value.ToString());
            ApplyReportFilters();
            OnPropertyChanged(propertyName);
            return true;
        }

        public int GroupAStage1Months { get => _groupAMonths[1]; set => SetGroupMonths(_groupAMonths, 1, SettingKeys.ReportGroupStageMonths("a", 1), value); }
        public int GroupAStage2Months { get => _groupAMonths[2]; set => SetGroupMonths(_groupAMonths, 2, SettingKeys.ReportGroupStageMonths("a", 2), value); }
        public int GroupAStage3Months { get => _groupAMonths[3]; set => SetGroupMonths(_groupAMonths, 3, SettingKeys.ReportGroupStageMonths("a", 3), value); }

        public int GroupBStage1Months { get => _groupBMonths[1]; set => SetGroupMonths(_groupBMonths, 1, SettingKeys.ReportGroupStageMonths("b", 1), value); }
        public int GroupBStage2Months { get => _groupBMonths[2]; set => SetGroupMonths(_groupBMonths, 2, SettingKeys.ReportGroupStageMonths("b", 2), value); }
        public int GroupBStage3Months { get => _groupBMonths[3]; set => SetGroupMonths(_groupBMonths, 3, SettingKeys.ReportGroupStageMonths("b", 3), value); }
        public int GroupBStage4Months { get => _groupBMonths[4]; set => SetGroupMonths(_groupBMonths, 4, SettingKeys.ReportGroupStageMonths("b", 4), value); }

        public int GroupCStage1Months { get => _groupCMonths[1]; set => SetGroupMonths(_groupCMonths, 1, SettingKeys.ReportGroupStageMonths("c", 1), value); }
        public int GroupCStage2Months { get => _groupCMonths[2]; set => SetGroupMonths(_groupCMonths, 2, SettingKeys.ReportGroupStageMonths("c", 2), value); }
        public int GroupCStage3Months { get => _groupCMonths[3]; set => SetGroupMonths(_groupCMonths, 3, SettingKeys.ReportGroupStageMonths("c", 3), value); }
        public int GroupCStage4Months { get => _groupCMonths[4]; set => SetGroupMonths(_groupCMonths, 4, SettingKeys.ReportGroupStageMonths("c", 4), value); }

        public int GroupDStage1Months { get => _groupDMonths[1]; set => SetGroupMonths(_groupDMonths, 1, SettingKeys.ReportGroupStageMonths("d", 1), value); }
        public int GroupDStage2Months { get => _groupDMonths[2]; set => SetGroupMonths(_groupDMonths, 2, SettingKeys.ReportGroupStageMonths("d", 2), value); }
        public int GroupDStage3Months { get => _groupDMonths[3]; set => SetGroupMonths(_groupDMonths, 3, SettingKeys.ReportGroupStageMonths("d", 3), value); }
        public int GroupDStage4Months { get => _groupDMonths[4]; set => SetGroupMonths(_groupDMonths, 4, SettingKeys.ReportGroupStageMonths("d", 4), value); }

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
                private set
                {
                    if (SetProperty(ref _isScanning, value))
                    {
                        (ScanCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                        (CancelScanCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    }
                }
            }

            private bool _isGenerating;
            /// <summary>تولید گزارش در حال اجراست؛ دکمه تولید در این مدت قفل است.</summary>
            public bool IsGenerating
            {
                get => _isGenerating;
                private set
                {
                    if (SetProperty(ref _isGenerating, value))
                    {
                        (GenerateCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
                        (CancelGenerateCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    }
                }
            }

            public ICommand ResetToDefaultsCommand { get; }
            public ICommand CancelScanCommand { get; }
            public ICommand CancelGenerateCommand { get; }

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

    public string CountDisplay => $"{ReportStatus.Count(n => n.IsSelected)} انتخاب‌شده از {ReportStatus.Count}";

    /// <summary>شمارنده «X انتخاب‌شده از Y» را بعد از تیک خوردن/برداشته شدن یک ردیف به‌روز می‌کند.</summary>
    public void RefreshCountDisplay() => OnPropertyChanged(nameof(CountDisplay));

    private bool _hasSelectedHistory;
    /// <summary>حداقل یک ردیف در تب تاریخچه انتخاب شده (تیک یا انتخاب در جدول).</summary>
    public bool HasSelectedHistory { get => _hasSelectedHistory; set => SetProperty(ref _hasSelectedHistory, value); }

    private int _selectedHistoryCount;
    public int SelectedHistoryCount
    {
        get => _selectedHistoryCount;
        set
        {
            if (SetProperty(ref _selectedHistoryCount, value))
                OnPropertyChanged(nameof(SelectedHistoryCountDisplay));
        }
    }

    /// <summary>شمارنده انتخاب در تب تاریخچه؛ وقتی صفر است خالی است.</summary>
    public string SelectedHistoryCountDisplay => SelectedHistoryCount > 0 ? $"{SelectedHistoryCount} انتخاب‌شده" : "";

    /// <summary>
    /// وضعیت انتخاب تب تاریخچه را از ویو می‌گیرد؛ ردیف‌های تیک‌خورده اولویت
    /// دارند وگرنه ردیف‌های انتخاب‌شده در جدول شمرده می‌شوند.
    /// </summary>
    public void UpdateHistorySelection(int checkedCount, int gridSelectedCount)
    {
        var total = checkedCount > 0 ? checkedCount : gridSelectedCount;
        SelectedHistoryCount = total;
        HasSelectedHistory = total > 0;
    }

    public ICommand BrowseTemplateCommand { get; }
    public ICommand BrowseOutputCommand { get; }
    public ICommand BrowseSignedTemplateCommand { get; }
    public ICommand ResetSignedTemplateCommand { get; }
    public ICommand ConvertToPdfCommand { get; }
    public ICommand ScanCommand { get; }
    public ICommand GenerateCommand { get; }
    public ICommand PreviewCommand { get; }
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
                _reportDate = PersianDateHelper.ToPersianDate(DateTime.Now);

                // P1/P2: signed templates + PDF generation (persisted in settings).
                _useSignedTemplates = _db.GetSetting(SettingKeys.SignedReport) == "1";
                var signedPath = _db.GetSetting(SettingKeys.SignedTemplatesPath);
                _signedTemplatePath = string.IsNullOrWhiteSpace(signedPath) ? Path.Combine(_templatePath, "signed") : signedPath;
                _generatePdf = _db.GetSetting(SettingKeys.GeneratePdf) == "1";
                _isScheduleExpanded = _db.GetSetting(SettingKeys.ScheduleExpanded) != "0";
                _isProductionExpanded = _db.GetSetting(SettingKeys.ProductionExpanded) == "1";

            // بارگذاری تنظیمات زمان‌بندی جدید (بر اساس مرحله و گروه)
            LoadSchedulingSettings();

            BrowseTemplateCommand = new RelayCommand(BrowseTemplate);
                        BrowseOutputCommand = new RelayCommand(BrowseOutput);
                        BrowseSignedTemplateCommand = new RelayCommand(BrowseSignedTemplate);
                        ResetSignedTemplateCommand = new RelayCommand(() => SignedTemplatePath = Path.Combine(_templatePath, "signed"));
                        ConvertToPdfCommand = new RelayCommand<ReportHistoryItem>(ConvertHistoryToPdf);
                        ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsScanning);
                        CancelScanCommand = new RelayCommand(CancelScan, () => IsScanning);
                        GenerateCommand = new AsyncRelayCommand(GenerateAsync, () => !IsGenerating);
                        CancelGenerateCommand = new RelayCommand(CancelGenerate, () => IsGenerating);
                        PreviewCommand = new RelayCommand(Preview);
                        ToggleScheduleCommand = new RelayCommand(() => IsScheduleExpanded = !IsScheduleExpanded);
                        ToggleProductionCommand = new RelayCommand(() => IsProductionExpanded = !IsProductionExpanded);
                        DownloadTemplatesCommand = new RelayCommand(OpenTemplateDownload);
                        OpenReportCommand = new RelayCommand<ReportHistoryItem>(OpenReport);
                        OpenFolderCommand = new RelayCommand<ReportHistoryItem>(OpenFolder);
                        RegenerateCommand = new RelayCommand<ReportHistoryItem>(RegenerateWithConfirmation);
                        DeleteReportCommand = new RelayCommand<ReportHistoryItem>(DeleteReportWithConfirmation);
                        SwitchToGenerateCommand = new RelayCommand(() => ActiveTab = 0);
                        SwitchToHistoryCommand = new RelayCommand(() => { ActiveTab = 1; LoadHistory(); });
                        DeleteLogCommand = new RelayCommand<ReportHistoryItem>(DeleteLog);
                        SyncOutputCommand = new RelayCommand(SyncOutputFolder);
                        ResetToDefaultsCommand = new RelayCommand(ResetSchedulingDefaults);

            LoadTemplates();
            LoadPreviousScanResults();
            UpdateStageConfigurations();
        }

        private void LoadSchedulingSettings()
                {
                    // One table drives all four groups: (months array, setting prefix, defaults per stage).
                    var groups = new (int[] Months, string Prefix, int[] Defaults)[]
                    {
                        (_groupAMonths, "a", new[] { 0, 2, 6, 10 }),
                        (_groupBMonths, "b", new[] { 0, 3, 6, 12, 20 }),
                        (_groupCMonths, "c", new[] { 0, 3, 8, 16, 24 }),
                        (_groupDMonths, "d", new[] { 0, 3, 8, 16, 30 }),
                    };
                    foreach (var (months, prefix, defaults) in groups)
                    {
                        for (int stage = 1; stage < defaults.Length; stage++)
                        {
                            months[stage] = int.TryParse(_db.GetSetting(SettingKeys.ReportGroupStageMonths(prefix, stage)), out var m) && m >= 1 && m <= 30
                                ? m
                                : defaults[stage];
                        }
                    }

                    // Per‑group migration: if a group’s stage‑1 setting is missing, copy the old group‑wide setting (if any)
                    int oldA = 2;
                    if (!string.IsNullOrEmpty(_db.GetSetting(SettingKeys.ReportIntervalGroupA)))
                    {
                        if (int.TryParse(_db.GetSetting(SettingKeys.ReportIntervalGroupA), out var parsed) && parsed >= 1 && parsed <= 30)
                            oldA = parsed;
                    }
                    int oldB = 3;
                    if (!string.IsNullOrEmpty(_db.GetSetting(SettingKeys.ReportIntervalGroupB)))
                    {
                        if (int.TryParse(_db.GetSetting(SettingKeys.ReportIntervalGroupB), out var parsed) && parsed >= 1 && parsed <= 30)
                            oldB = parsed;
                    }

                    // Group A
                    if (string.IsNullOrEmpty(_db.GetSetting(SettingKeys.ReportGroupStageMonths("a", 1))))
                    {
                        _groupAMonths[1] = oldA;
                        _groupAMonths[2] = oldA;
                        _groupAMonths[3] = oldA;
                        // persist so we don’t migrate again
                        _db.SaveSetting(SettingKeys.ReportGroupStageMonths("a", 1), oldA.ToString());
                        _db.SaveSetting(SettingKeys.ReportGroupStageMonths("a", 2), oldA.ToString());
                        _db.SaveSetting(SettingKeys.ReportGroupStageMonths("a", 3), oldA.ToString());
                    }

                    // Group B
                    if (string.IsNullOrEmpty(_db.GetSetting(SettingKeys.ReportGroupStageMonths("b", 1))))
                    {
                        _groupBMonths[1] = oldB;
                        _groupBMonths[2] = oldB;
                        _groupBMonths[3] = oldB;
                        _groupBMonths[4] = oldB;
                        _db.SaveSetting(SettingKeys.ReportGroupStageMonths("b", 1), oldB.ToString());
                        _db.SaveSetting(SettingKeys.ReportGroupStageMonths("b", 2), oldB.ToString());
                        _db.SaveSetting(SettingKeys.ReportGroupStageMonths("b", 3), oldB.ToString());
                        _db.SaveSetting(SettingKeys.ReportGroupStageMonths("b", 4), oldB.ToString());
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
    /// ساخت مولد گزارش با تمام مسیرهای قالب: پوشه خود برنامه + مسیرهای نسخه‌های قبلی
    /// (مثل Downloads\NezamMonitor-Windows\templates) + مسیرهای قابل‌تنظیم در پایگاه‌داده.
    /// </summary>
    private StageReportGenerator CreateGenerator(string? primaryDir = null)
    {
        var generator = new StageReportGenerator(primaryDir ?? _templatePath);
        generator.UserName = _db.GetSetting(SettingKeys.ApiUserName) ?? "";
        generator.UseSignedTemplates = UseSignedTemplates;
        generator.SetSignedTemplateDirectory(SignedTemplatePath);

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

            // مسیر نسخه قبلی/سورس روی درایو E
            dirs.Add(@"E:\Nezam Monitor-Mahdi2\Source_and_Development\templates");
            dirs.Add(@"E:\NezamMonitor-Mahdi2\Source_and_Development\templates");
            dirs.Add(@"E:\Nezam Monitor-Mahdi2\templates");
        }
        catch { }

        // مسیر دلخواه کاربر: template_paths=D:\a\templates;D:\b\templates
        try
        {
            var extra = _db.GetSetting(SettingKeys.TemplatePaths);
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

    /// <summary>انتخاب پوشه تمپلیت‌های امضاشده (P1).</summary>
    private void BrowseSignedTemplate()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "انتخاب پوشه تمپلیت‌های امضاشده",
            InitialDirectory = Directory.Exists(SignedTemplatePath) ? SignedTemplatePath : _templatePath
        };
        if (dialog.ShowDialog() == true)
            SignedTemplatePath = dialog.FolderName;
    }

    /// <summary>Converts one generated Word file to PDF and records it in the
    /// history (P2/V5). Returns "" on success, " (PDF نشد: ...)" on failure.</summary>
    private string ConvertWith(WordToPdfConverter converter, string caseNumber, int stage, string wordPath)
    {
        var pdfPath = Path.ChangeExtension(wordPath, ".pdf");
        if (!converter.Convert(wordPath, pdfPath, out var error))
            return $" (PDF نشد: {error})";
        _db.SetGeneratedReportPdf(caseNumber, stage, pdfPath);
        return "";
    }

    /// <summary>تبدیل یک گزارش موجود در سابقه به PDF (P2).</summary>
    private void ConvertHistoryToPdf(ReportHistoryItem? item)
    {
        if (item == null || string.IsNullOrEmpty(item.OutputPath) || !File.Exists(item.OutputPath))
        {
            StatusMessage = "فایل Word یافت نشد";
            return;
        }
        var pdfError = ConvertOneToPdf(item);
        StatusMessage = string.IsNullOrEmpty(pdfError)
            ? $"PDF ساخته شد: {item.CaseNumber} مرحله {item.StageName}"
            : $"PDF ساخته نشد: {pdfError.Trim()}";
        LoadHistory();
    }

    /// <summary>Convert one history item to PDF. Returns the error, or empty on success. No grid reload.</summary>
    private string ConvertOneToPdf(ReportHistoryItem item)
    {
        using var pdfConverter = new WordToPdfConverter();
        if (!pdfConverter.IsAvailable)
            return $"PDF ساخته نمی‌شود: {pdfConverter.Error}";
        return ConvertWith(pdfConverter, item.CaseNumber, item.Stage, item.OutputPath);
    }

    /// <summary>Convert the selected history rows to PDF (one pass, one grid reload).</summary>
    public void ConvertSelectedToPdf(List<ReportHistoryItem> items)
    {
        if (items == null || items.Count == 0) { StatusMessage = "موردی انتخاب نشده"; return; }
        int done = 0, errors = 0;
        string lastError = "";
        foreach (var item in items)
        {
            if (string.IsNullOrEmpty(item.OutputPath) || !File.Exists(item.OutputPath)) { errors++; continue; }
            var err = ConvertOneToPdf(item);
            if (string.IsNullOrEmpty(err)) done++; else { errors++; lastError = err; }
        }
        StatusMessage = errors == 0
            ? $"{done} PDF ساخته شد"
            : $"{done} PDF ساخته شد، {errors} نشد" + (string.IsNullOrEmpty(lastError) ? "" : $": {lastError.Trim()}");
        LoadHistory();
    }

    /// <summary>Open the selected history rows (each file in its own app).</summary>
    public void OpenSelectedHistory(List<ReportHistoryItem> items)
    {
        if (items == null || items.Count == 0) { StatusMessage = "موردی انتخاب نشده"; return; }
        if (items.Count > 1)
        {
            var confirm = MessageBox.Show(
                $"{items.Count} گزارش یک‌جا باز شود؟",
                "باز کردن چند گزارش", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;
        }
        int done = 0, missing = 0;
        foreach (var item in items)
        {
            if (!string.IsNullOrEmpty(item.OutputPath) && File.Exists(item.OutputPath))
            {
                Process.Start(new ProcessStartInfo { FileName = item.OutputPath, UseShellExecute = true });
                done++;
            }
            else missing++;
        }
        StatusMessage = missing == 0
            ? $"{done} گزارش باز شد"
            : $"{done} گزارش باز شد، {missing} فایل یافت نشد";
    }

    /// <summary>Open the folders of the selected history rows.</summary>
    public void OpenSelectedHistoryFolder(List<ReportHistoryItem> items)
    {
        if (items == null || items.Count == 0) { StatusMessage = "موردی انتخاب نشده"; return; }
        int done = 0;
        foreach (var item in items)
        {
            OpenFolder(item);
            done++;
        }
        StatusMessage = $"{done} پوشه باز شد";
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
            OutputPath = dialog.FolderName;
        }
    }

    private void OpenTemplateDownload()
    {
        var window = new Views.TemplateDownloadWindow
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        window.ShowDialog();
    }

    /// <summary>
    /// وضعیت «تولیدشده» است؟ هم شکل جدید («تولیدشده») و هم شکل قدیمی
    /// ذخیره‌شده در دیتابیس («تولید شده») را می‌پذیرد.
    /// </summary>
    internal static bool IsProducedStatus(string? status)
        => !string.IsNullOrEmpty(status) && (status.Contains("تولیدشده") || status.Contains("تولید شده"));

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
            var snapshotId = _db.GetActiveSnapshotId();
            var cases = _db.LoadCases(snapshotId);
            var rowMap = new Dictionary<string, int>(cases.Count);
            for (int i = 0; i < cases.Count; i++)
                rowMap[cases[i].CaseNumber] = i + 1; // 1-based, matches CasesView order

            // گزارش‌های سامانه از همان پرونده‌های لودشده (بدون لود دوباره)؛
            // کش هم با همین داده پر می‌شود تا اسکن بعدی دوباره نخواند.
            var userName = CurrentUserNameNormalized();
            _serverLookup = BuildServerLookup(cases, userName);
            _serverLookupSnapshotId = snapshotId;
            _serverLookupUserName = userName;

            _allReportItems.Clear();
            ReportStatus.Clear();

            foreach (var s in saved)
            {
                // GeneratedAt در دیتابیس باید میلادی باشد، ولی ردیف‌های قدیمی ممکن است
                // شمسی ذخیره شده باشند؛ اول به میلادی نرمال می‌کنیم و بعد شمسی را می‌سازیم.
                var gregorianAt = PersianDateHelper.NormalizeToGregorian(s.GeneratedAt);
                var shamsi = PersianDateHelper.GregoryStringToShamsi(gregorianAt);
                // در سامانه هست؟ وضعیت ذخیره‌شده ممکن است قدیمی باشد (قبل از این ویژگی)؛
                // اگر سامانه گزارش را دارد، وضعیت «تولیدشده» است.
                var serverReport = LatestServerReport(_serverLookup, s.CaseNumber, s.Stage);
                var inServer = serverReport != null;
                var generated = IsProducedStatus(s.Status) || inServer;
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
                    Status = inServer ? "✅ تولیدشده (سامانه)" : generated ? "✅ تولیدشده (سیستم)" : "⏳ در انتظار",
                    ServerDate = serverReport?.VisitDate ?? "",
                    IsServerReport = inServer,
                    GeneratedAt = gregorianAt,
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
            _loadedScanSnapshotId = snapshotId;
            StatusMessage = $"نتایج اسکن قبلی بارگذاری شد ({_allReportItems.Count} مورد). اسکن مجدد برای بروزرسانی.";
        }
        catch { /* silently ignore - scan will show fresh results */ }
    }

    /// <summary>اگر از آخرین اسکن، اسنپ‌شات فعال عوض شده، نتایج اسکن قبلی را دوباره بخوان.</summary>
    public void RefreshIfSnapshotChanged()
    {
        if (_loadedScanSnapshotId != _db.GetActiveSnapshotId())
            LoadPreviousScanResults();
    }

    /// <summary>
        /// اسکن پرونده‌ها و نمایش وضعیت واقعی گزارش‌ها (Async با پشتیبانی از لغو)
        /// </summary>
        private async Task ScanAsync()
        {
            if (IsScanning) return;
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
                StatusMessage = "اسکن با خطا مواجه شد؛ جزییات در فایل لاگ ثبت شد.";
                ErrorLog.Write(ex, "GeneratorViewModel.ScanAsync");
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

        /// <summary>لغو اسکن در حال اجرا (دکمه «لغو اسکن»).</summary>
        private void CancelScan() => _scanCts?.Cancel();

        /// <summary>
        /// ردیف‌های اسکن‌شده را دسته‌ای به UI می‌فرستد؛ یک Invoke برای چند ردیف
        /// و پیشرفت، نه یک Invoke برای هر ردیف.
        /// </summary>
        private void FlushScanBatch(List<CaseReportItem> pending, int progress, string progressText)
        {
            if (pending.Count == 0) return;
            var batch = pending.ToArray();
            pending.Clear();
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var item in batch) _allReportItems.Add(item);
                Progress = progress;
                ProgressText = progressText;
            });
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

            // دریافت تمام گزارش‌های تولیدشده از دیتابیس
            var existingReports = _db.GetGeneratedReports();
            var generatedDates = new Dictionary<string, string>();
            foreach (var r in existingReports)
            {
                var key = $"{r.CaseNumber}|{r.Stage}";
                if (!generatedDates.ContainsKey(key) && !string.IsNullOrEmpty(r.CreatedAt))
                    generatedDates[key] = r.CreatedAt;
            }

            // گزارش‌های سامانه (کش‌شده بر اساس اسنپ‌شات) — برای وضعیت و تاریخ سامانه.
            var server = GetServerLookup();

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
            // ردیف‌های تازه‌اسکن‌شده اینجا جمع می‌شوند و دسته‌ای به UI می‌روند.
            var pendingItems = new List<CaseReportItem>();
            foreach (var c in cases)
            {
                ct.ThrowIfCancellationRequested();
                current++;

                // Progress is applied on the UI thread together with the
                // batched item flush below (not one Invoke per case).
                var progress = (int)((double)current / cases.Count * 100);
                var progressText = $"{current}/{cases.Count}";

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

                    // در سامانه هست؟ تازه‌ترین گزارش سامانه برای این (پرونده، مرحله).
                    // وضعیت «تولیدشده» یعنی روی سیستم ساخته شده یا در سامانه ثبت شده.
                    var serverReport = LatestServerReport(server, c.CaseNumber, stage);
                    var inServer = serverReport != null;
                    var generated = hasReport || inServer;
                    var deadlineStatus = GetDeadlineStatus(generated, daysRemaining);

                    // تاریخ تولید — در دیتابیس میلادی ذخیره می‌شود، ولی ردیف‌های قدیمی
                    // ممکن است شمسی ذخیره شده باشند؛ اول به میلادی نرمال می‌کنیم.
                    var generatedAt = "";
                    if (hasReport && generatedDates.TryGetValue(reportKey, out var gDate))
                        generatedAt = PersianDateHelper.NormalizeToGregorian(gDate);

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
                        Status = inServer ? "✅ تولیدشده (سامانه)" : generated ? "✅ تولیدشده (سیستم)" : "⏳ در انتظار",
                        ServerDate = serverReport?.VisitDate ?? "",
                        IsServerReport = inServer,
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
                
                    // Add to the batch; flushed to the UI thread in chunks.
                    pendingItems.Add(item);
                    if (pendingItems.Count >= 25)
                        FlushScanBatch(pendingItems, progress, progressText);
                }
            }

            // Flush any rows still waiting in the batch.
            FlushScanBatch(pendingItems, 100, $"{cases.Count}/{cases.Count}");

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
                HasReport = IsProducedStatus(item.Status),
            }).ToList();
            _db.SaveLastScanResults(scanResults);
            _loadedScanSnapshotId = _db.GetActiveSnapshotId();

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

        // فیلتر وضعیت: 0=همه, 1=در انتظار, 2=تولیدشده
        if (FilterStatus == 1)
            filtered = filtered.Where(r => r.Status.Contains("در انتظار"));
        else if (FilterStatus == 2)
            filtered = filtered.Where(r => IsProducedStatus(r.Status));

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

    private void Preview()
    {
        var selected = ReportStatus.Where(r => r.IsSelected).ToList();
        if (selected.Count == 0)
        {
            PreviewText = "موردی انتخاب نشده";
            return;
        }

        // Rows already produced are not generated again in a batch; rebuild
        // them from the history tab («تولید مجدد») or the row's context menu.
        var toGenerate = selected.Where(r => !IsProducedStatus(r.Status)).ToList();
        var skipped = selected.Count - toGenerate.Count;

        if (toGenerate.Count == 0)
        {
            PreviewText = "همه موارد انتخاب‌شده قبلا تولید شده‌اند و دوباره ساخته نمی‌شوند؛ برای تولید مجدد از تب «گزارش‌های تولیدشده» استفاده کن.";
            return;
        }

        var ltrSuffix = UseLtr ? " - LTR" : "";
        var lines = new List<string> { $"گزارش‌های آماده تولید ({toGenerate.Count} مورد):", "" };

        foreach (var item in toGenerate)
        {
            var folderName = StageReportRules.GenerateFolderName(item.RowNumber, new Case { CaseNumber = item.CaseNumber, Owner = item.Owner });
            lines.Add($"- {item.Owner} ({item.StageName}), {item.CaseNumber} -> {folderName}{ltrSuffix}/گزارش مرحله {item.Stage}{ltrSuffix}.docx");
        }

        if (skipped > 0)
        {
            lines.Add("");
            lines.Add($"{skipped} مورد چون قبلا تولید شده‌اند ساخته نمی‌شوند؛ برای تولید مجدد از تب «گزارش‌های تولیدشده» استفاده کن.");
        }

        PreviewText = string.Join("\r\n", lines);
    }

    /// <summary>
    /// تولید گزارش‌های انتخاب شده — غیرهم‌زمان: ساخت Word روی نخ پس‌زمینه،
    /// تبدیل PDF روی یک نخ STA جدا (Word با COM فقط روی STA کار می‌کند).
    /// </summary>
    private async Task GenerateAsync()
    {
        if (IsGenerating) return;

        // Capture the selection synchronously: the context menu clears
        // IsSelected right after Execute returns, so the snapshot must
        // happen before the first await.
        var selected = ReportStatus.Where(r => r.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "هیچ موردی انتخاب نشده";
            return;
        }

        _generateCts = new CancellationTokenSource();
        var ct = _generateCts.Token;
        IsGenerating = true;
        Progress = 0;
        ProgressText = "";
        StatusMessage = "در حال تولید گزارش‌ها...";

        try
        {
            var result = await Task.Run(() => GenerateBatch(selected, ct), ct);
            if (result.cancelled)
            {
                StatusMessage = $"تولید لغو شد؛ {result.generated} تولید شد و {result.remaining} مورد ناتمام ماند. فایل نیمه‌کاره‌ای باقی نماند.";
                Progress = 0;
            }
            else
            {
                var parts = new List<string> { $"{result.generated} تولید شد", $"{result.failed} خطا" };
                if (result.skipped > 0)
                    parts.Add($"{result.skipped} مورد از قبل تولید شده بود");
                StatusMessage = "تکمیل: " + string.Join(" | ", parts) + $" | مسیر: {_outputPath}";
                Progress = 100;
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "تولید لغو شد؛ فایل نیمه‌کاره‌ای باقی نماند.";
            Progress = 0;
        }
        catch (Exception ex)
        {
            StatusMessage = "تولید گزارش با خطا مواجه شد؛ جزییات در فایل لاگ ثبت شد.";
            ErrorLog.Write(ex, "GeneratorViewModel.Generate");
            Progress = 0;
        }
        finally
        {
            IsGenerating = false;
            _generateCts?.Dispose();
            _generateCts = null;
            LoadHistory();
        }
    }

    /// <summary>لغو تولید در حال اجرا (دکمه «لغو تولید»).</summary>
    private void CancelGenerate() => _generateCts?.Cancel();

    /// <summary>
    /// The heavy batch work: runs on a background thread. Word generation
    /// (Open XML) is thread-safe; PDF conversion runs on a dedicated STA
    /// thread because Word COM requires STA. UI-bound item properties are
    /// updated through the Dispatcher.
    /// Returns (generated, failed, skipped-already-produced, cancelled, remaining).
    /// </summary>
    private (int generated, int failed, int skipped, bool cancelled, int remaining) GenerateBatch(List<CaseReportItem> selected, CancellationToken ct)
    {
        Directory.CreateDirectory(_outputPath);
        var cases = _db.LoadCases(_db.GetActiveSnapshotId());

        // One-time lookup maps (O(N), not a linear search per row).
        var caseMap = new Dictionary<string, Case>(cases.Count);
        var rowMap = new Dictionary<string, int>(cases.Count);
        for (int i = 0; i < cases.Count; i++)
        {
            if (!caseMap.ContainsKey(cases[i].CaseNumber)) caseMap[cases[i].CaseNumber] = cases[i];
            if (!rowMap.ContainsKey(cases[i].CaseNumber)) rowMap[cases[i].CaseNumber] = i + 1;
        }

        var generator = CreateGenerator();
        var dispatcher = Application.Current.Dispatcher;

        // PDF conversion needs an STA thread (Word COM); it must NOT run on
        // a plain Task.Run thread pool thread.
        using var staWorker = GeneratePdf ? new StaWorker() : null;
        WordToPdfConverter? pdfConverter = null;
        if (staWorker != null)
        {
            staWorker.Run(() => { pdfConverter = new WordToPdfConverter(); });
            if (pdfConverter != null && !pdfConverter.IsAvailable)
                dispatcher.Invoke(() => StatusMessage = $"PDF ساخته نمی‌شود: {pdfConverter.Error}");
        }

        int generated = 0, failed = 0, skipped = 0, current = 0;
        var total = selected.Count;

        try
        {
            foreach (var item in selected)
            {
                ct.ThrowIfCancellationRequested();
                current++;
                var progress = (int)((double)current / total * 100);
                var progressText = $"{current}/{total}";

                // Rows already produced are skipped in a batch; rebuild them
                // from the history tab («تولید مجدد») or the row's context menu.
                if (IsProducedStatus(item.Status))
                {
                    skipped++;
                    dispatcher.Invoke(() => { Progress = progress; ProgressText = progressText; });
                    continue;
                }

                string? wordPath = null;
                string? pdfPath = null;
                try
                {
                    if (!caseMap.TryGetValue(item.CaseNumber, out var c))
                    {
                        failed++;
                        dispatcher.Invoke(() => { item.Status = "❌ پرونده یافت نشد"; Progress = progress; ProgressText = progressText; });
                        continue;
                    }

                    if (!rowMap.TryGetValue(c.CaseNumber, out var rowNumber) || rowNumber <= 0)
                    {
                        failed++;
                        dispatcher.Invoke(() => { item.Status = $"❌ پرونده {c.CaseNumber} در لیست پرونده‌ها یافت نشد (ردیف نامعتبر)"; Progress = progress; ProgressText = progressText; });
                        continue;
                    }

                    var templateForStage = generator.FindTemplateForStage(item.Stage);
                    if (templateForStage == null)
                    {
                        failed++;
                        dispatcher.Invoke(() => { item.Status = $"❌ تمپلیت مرحله {item.StageName} یافت نشد"; Progress = progress; ProgressText = progressText; });
                        continue;
                    }

                    var genResult = generator.GenerateReport(c, item.Stage, rowNumber, _outputPath, DateTime.Now, templateForStage, UseLtr);

                    if (!genResult.Success || genResult.OutputPath == null || !File.Exists(genResult.OutputPath) || new FileInfo(genResult.OutputPath).Length == 0)
                    {
                        failed++;
                        var err = genResult.Success ? "❌ فایل ایجاد نشد" : $"❌ {genResult.ErrorMessage}";
                        dispatcher.Invoke(() => { item.Status = err; Progress = progress; ProgressText = progressText; });
                        continue;
                    }

                    wordPath = genResult.OutputPath;

                    // Cancelled while this file was being built: drop the
                    // partial file instead of recording it.
                    if (ct.IsCancellationRequested)
                    {
                        TryDeleteFile(wordPath);
                        throw new OperationCanceledException();
                    }

                    string pdfSuffix = "";
                    if (pdfConverter != null && pdfConverter.IsAvailable)
                    {
                        pdfPath = Path.ChangeExtension(wordPath, ".pdf");
                        var converter = pdfConverter;
                        var wPath = wordPath;
                        var pPath = pdfPath;
                        string? pdfError = null;
                        var pdfOk = false;
                        staWorker!.Run(() => { pdfOk = converter.Convert(wPath, pPath, out pdfError); });
                        if (pdfOk)
                        {
                            pdfSuffix = " + PDF";
                        }
                        else
                        {
                            pdfSuffix = $" (PDF نشد: {pdfError})";
                            pdfPath = null; // failed conversion leaves no usable PDF
                        }
                    }

                    if (ct.IsCancellationRequested)
                    {
                        TryDeleteFile(wordPath);
                        TryDeleteFile(pdfPath);
                        throw new OperationCanceledException();
                    }

                    var numFormat = UseLtr ? "LTR" : "Persian";
                    var fileHash = NezamDatabase.ComputeFileHash(wordPath);
                    _db.SaveGeneratedReport(c.CaseNumber, item.Stage, Path.GetFileNameWithoutExtension(templateForStage), wordPath, c.Owner, numFormat, fileHash);
                    if (pdfPath != null)
                        _db.SetGeneratedReportPdf(c.CaseNumber, item.Stage, pdfPath);

                    var now2 = DateTime.Now;
                    // مهم: بدون InvariantCulture، با فرهنگ fa-IR این رشته شمسی می‌شود.
                    var generatedAt = now2.ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);
                    var generatedAtShamsi = PersianDateHelper.ToShamsiDateTime(now2);
                    var signedSuffix = generator.IsSignedTemplate(templateForStage) ? " (امضا شده)" : "";
                    generated++;
                    dispatcher.Invoke(() =>
                    {
                        item.Status = "✅ تولیدشده" + signedSuffix + pdfSuffix;
                        item.GeneratedAt = generatedAt;
                        item.GeneratedAtShamsi = generatedAtShamsi;
                        item.IsSelected = false;
                        Progress = progress;
                        ProgressText = progressText;
                    });
                }
                catch (OperationCanceledException)
                {
                    TryDeleteFile(wordPath);
                    TryDeleteFile(pdfPath);
                    throw;
                }
            }
        }
        catch (OperationCanceledException)
        {
            return (generated, failed, skipped, true, total - current);
        }
        finally
        {
            if (pdfConverter != null && staWorker != null)
            {
                var converter = pdfConverter;
                staWorker.Run(() => converter.Dispose());
            }
        }

        return (generated, failed, skipped, false, 0);
    }

    private static void TryDeleteFile(string? path)
    {
        try { if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path); } catch { }
    }

    /// <summary>
    /// A dedicated STA thread with its own Dispatcher (message pump) for
    /// Word COM calls. Word automation requires STA; a Task.Run thread is
    /// MTA and would break conversion.
    /// </summary>
    private sealed class StaWorker : IDisposable
    {
        private readonly Thread _thread;
        private Dispatcher? _dispatcher;
        private readonly ManualResetEventSlim _ready = new(false);
        private bool _disposed;

        public StaWorker()
        {
            _thread = new Thread(() =>
            {
                _dispatcher = Dispatcher.CurrentDispatcher;
                _ready.Set();
                Dispatcher.Run();
            })
            { IsBackground = true, Name = "NezamMonitor PDF STA" };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
            _ready.Wait();
        }

        public void Run(Action action)
        {
            if (_dispatcher == null) throw new InvalidOperationException("نخ STA آماده نیست.");
            _dispatcher.Invoke(action);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _dispatcher?.InvokeShutdown(); } catch { }
            _thread.Join(TimeSpan.FromSeconds(30));
            _ready.Dispose();
        }
    }

    /// <summary>
    /// بارگذاری تاریخچه گزارش‌های تولیدشده
    /// </summary>
    public void LoadHistory()
    {
        try
        {
            ReportHistory.Clear();
            HasSelectedHistory = false;
            SelectedHistoryCount = 0;
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
                    PdfPath = r.PdfPath,
                    FileType = r.FileType,
                    Status = status,
                    FileExists = fileExists,
                    FolderPath = folderPath
                });
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "بارگذاری تاریخچه با خطا مواجه شد؛ جزییات در فایل لاگ ثبت شد.";
            ErrorLog.Write(ex, "GeneratorViewModel.LoadHistory");
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
    /// Server reports grouped by case number, from already-loaded cases.
    /// </summary>
    private sealed record ServerReportInfo(int Stage, string VisitDate, string Title, string Owner);

    private int _serverLookupSnapshotId = -1;
    private string _serverLookupUserName = "";
    private Dictionary<string, List<ServerReportInfo>> _serverLookup = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The logged-in user's name, normalized for comparison (Arabic→Persian,
    /// whitespace collapsed). Read fresh every time — it only exists after an
    /// update, which can happen after this ViewModel was created.
    /// </summary>
    private string CurrentUserNameNormalized()
        => FilterHelper.Normalize(_db.GetSetting(SettingKeys.ApiUserName));

    /// <summary>
    /// The user's OWN server reports grouped by case number, from already-loaded cases.
    /// Only reports whose engineer is the logged-in user count — a case's other
    /// engineers' reports must not mark this user's report as produced.
    /// With no known user name, nothing can be attributed, so the map is empty
    /// and the status falls back to local files only.
    /// </summary>
    private static Dictionary<string, List<ServerReportInfo>> BuildServerLookup(List<Case> cases, string userNameNormalized)
    {
        var map = new Dictionary<string, List<ServerReportInfo>>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(userNameNormalized)) return map;
        foreach (var c in cases)
        {
            foreach (var r in c.Reports)
            {
                if (!string.Equals(FilterHelper.Normalize(r.Engineer), userNameNormalized, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!map.TryGetValue(c.CaseNumber, out var list))
                    map[c.CaseNumber] = list = new List<ServerReportInfo>();
                list.Add(new ServerReportInfo(ParseStageNumber(r.Stage), r.VisitDate, r.Stage, c.Owner));
            }
        }
        return map;
    }

    /// <summary>
    /// The user's own server reports grouped by case number, rebuilt when the
    /// active snapshot changes or when the user's name becomes known — scans
    /// and reloads reuse the cache.
    /// </summary>
    private Dictionary<string, List<ServerReportInfo>> GetServerLookup()
    {
        var snapshotId = _db.GetActiveSnapshotId();
        var userName = CurrentUserNameNormalized();
        if (snapshotId == _serverLookupSnapshotId && userName == _serverLookupUserName) return _serverLookup;
        _serverLookupSnapshotId = snapshotId;
        _serverLookupUserName = userName;
        _serverLookup = snapshotId != 0
            ? BuildServerLookup(_db.LoadCases(snapshotId), userName)
            : new Dictionary<string, List<ServerReportInfo>>(StringComparer.OrdinalIgnoreCase);
        return _serverLookup;
    }

    /// <summary>
    /// Server stage titles look like "مرحله اول: اجرای فنداسیون" — the
    /// ordinal word gives the stage number (0 when it cannot be parsed).
    /// </summary>
    private static int ParseStageNumber(string? stageTitle)
    {
        if (string.IsNullOrWhiteSpace(stageTitle)) return 0;
        var t = FilterHelper.Normalize(stageTitle);
        if (t.Contains("اول")) return 1;
        if (t.Contains("دوم")) return 2;
        if (t.Contains("سوم")) return 3;
        if (t.Contains("چهارم")) return 4;
        return 0;
    }

    /// <summary>
    /// Latest server report for one (case, stage), or null when the سامانه
    /// has none — the most recent registration date wins.
    /// </summary>
    private static ServerReportInfo? LatestServerReport(
        Dictionary<string, List<ServerReportInfo>> server, string caseNumber, int stage)
    {
        if (!server.TryGetValue(caseNumber, out var list)) return null;
        return list.Where(s => s.Stage == stage)
                   .OrderByDescending(s => s.VisitDate, StringComparer.Ordinal)
                   .FirstOrDefault();
    }

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
            StatusMessage = "حذف گزارش با خطا مواجه شد؛ جزییات در فایل لاگ ثبت شد.";
            ErrorLog.Write(ex, "GeneratorViewModel.DeleteHistory");
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
            StatusMessage = "حذف لاگ با خطا مواجه شد؛ جزییات در فایل لاگ ثبت شد.";
            ErrorLog.Write(ex, "GeneratorViewModel.DeleteLog");
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

        var confirmMsg = $"این گزارش قبلا تولید شده است.\n\nآیا می‌خواهید آن را مجددا تولید کنید?\n\nمالک: {item.OwnerName}\nمرحله: {item.StageName}";
        var confirm = MessageBox.Show(confirmMsg, "تولید مجدد", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
        {
            StatusMessage = "لغو تولید مجدد";
            return;
        }

        if (TryRegenerate(item, out var error))
        {
            StatusMessage = $"تولید مجدد: {item.CaseNumber} مرحله {item.StageName} ✅";
            LoadHistory();
        }
        else
        {
            StatusMessage = $"خطا: {error}";
        }
    }

    /// <summary>تولید مجدد یک ردیف جدول تولید که قبلا تولید شده (با تایید)؛
    /// تولید دسته‌ای چنین ردیف‌هایی را رد می‌کند، پس منوی راست‌کلیک اینجا را صدا می‌زند.</summary>
    public void RegenerateCaseReport(CaseReportItem item)
    {
        if (item == null) { StatusMessage = "موردی انتخاب نشده"; return; }

        var confirmMsg = $"این گزارش قبلا تولید شده است.\n\nآیا می‌خواهید آن را مجددا تولید کنید?\n\nمالک: {item.Owner}\nمرحله: {item.StageName}";
        var confirm = MessageBox.Show(confirmMsg, "تولید مجدد", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
        {
            StatusMessage = "لغو تولید مجدد";
            return;
        }

        // Keep the original direction (LTR/Persian) of the generated report.
        var existing = _db.GetGeneratedReports().FirstOrDefault(r => r.CaseNumber == item.CaseNumber && r.Stage == item.Stage);
        var historyItem = new ReportHistoryItem
        {
            CaseNumber = item.CaseNumber,
            Stage = item.Stage,
            NumberFormat = existing.NumberFormat ?? "Persian",
            OwnerName = item.Owner
        };

        if (TryRegenerate(historyItem, out var error))
        {
            StatusMessage = $"تولید مجدد: {item.CaseNumber} مرحله {item.StageName} ✅";
            LoadHistory();
        }
        else
        {
            StatusMessage = $"خطا: {error}";
        }
    }

    /// <summary>Regenerate the selected history rows — one confirmation for all, one grid reload.</summary>
    public void RegenerateSelectedHistory(List<ReportHistoryItem> items)
    {
        if (items == null || items.Count == 0) { StatusMessage = "موردی انتخاب نشده"; return; }

        var confirmMsg = items.Count == 1
            ? $"این گزارش قبلا تولید شده است.\n\nآیا می‌خواهید آن را مجددا تولید کنید?\n\nمالک: {items[0].OwnerName}\nمرحله: {items[0].StageName}"
            : $"آیا می‌خواهید {items.Count} گزارش انتخاب‌شده را مجددا تولید کنید?";
        var confirm = MessageBox.Show(confirmMsg, "تولید مجدد", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) { StatusMessage = "لغو تولید مجدد"; return; }

        int done = 0; string lastError = "";
        foreach (var item in items)
        {
            if (TryRegenerate(item, out var error)) done++;
            else lastError = error;
        }
        StatusMessage = done == items.Count
            ? $"{done} گزارش مجددا تولید شد ✅"
            : $"{done} از {items.Count} گزارش تولید شد" + (string.IsNullOrEmpty(lastError) ? "" : $" — خطا: {lastError}");
        LoadHistory();
    }

    /// <summary>Regenerate one history item (no dialog, no grid reload). Returns false + error on failure.</summary>
    private bool TryRegenerate(ReportHistoryItem item, out string error)
    {
        error = "";
        var cases = _db.LoadCases(_db.GetActiveSnapshotId());

        // One-time lookup maps (O(N), not a linear search per row).
        var caseMap = new Dictionary<string, Case>(cases.Count);
        var rowMap = new Dictionary<string, int>(cases.Count);
        for (int i = 0; i < cases.Count; i++)
        {
            if (!caseMap.ContainsKey(cases[i].CaseNumber)) caseMap[cases[i].CaseNumber] = cases[i];
            if (!rowMap.ContainsKey(cases[i].CaseNumber)) rowMap[cases[i].CaseNumber] = i + 1;
        }

        if (!caseMap.TryGetValue(item.CaseNumber, out var c)) { error = "پرونده یافت نشد"; return false; }

        var rowNumber = rowMap[item.CaseNumber];
        var isLtr = item.NumberFormat == "LTR";
        var generator = CreateGenerator(Path.GetDirectoryName(SelectedTemplatePath) ?? _templatePath);
        var genResult = generator.GenerateReport(c, item.Stage, rowNumber, _outputPath, DateTime.Now, null, isLtr);

        if (genResult.Success && genResult.OutputPath != null && File.Exists(genResult.OutputPath))
        {
            _db.DeleteGeneratedReport(c.CaseNumber, item.Stage);
            var numFormat = isLtr ? "LTR" : "Persian";
            var fileHash = NezamDatabase.ComputeFileHash(genResult.OutputPath);
            _db.SaveGeneratedReport(c.CaseNumber, item.Stage, SelectedTemplateName, genResult.OutputPath, c.Owner, numFormat, fileHash);
            if (GeneratePdf)
            {
                using var pdfConverter = new WordToPdfConverter();
                if (!pdfConverter.IsAvailable)
                    error = $" (PDF نشد: {pdfConverter.Error})";
                else
                {
                    var pdfError = ConvertWith(pdfConverter, c.CaseNumber, item.Stage, genResult.OutputPath);
                    if (!string.IsNullOrEmpty(pdfError)) error = pdfError;
                }
            }
            return true;
        }

        error = genResult.ErrorMessage;
        return false;
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
                StatusMessage = "همگام‌سازی پوشه خروجی با خطا مواجه شد؛ جزییات در فایل لاگ ثبت شد.";
                ErrorLog.Write(ex, "GeneratorViewModel.SyncOutputFolder");
            }
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
                            _db.SaveSetting(SettingKeys.ReportGroupStageMonths("a", 1), value.ToString());
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
                            _db.SaveSetting(SettingKeys.ReportGroupStageMonths("a", 2), value.ToString());
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
                            _db.SaveSetting(SettingKeys.ReportGroupStageMonths("a", 3), value.ToString());
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
                            _db.SaveSetting(SettingKeys.ReportGroupStageMonths("b", 1), value.ToString());
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
                            _db.SaveSetting(SettingKeys.ReportGroupStageMonths("b", 2), value.ToString());
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
                            _db.SaveSetting(SettingKeys.ReportGroupStageMonths("b", 3), value.ToString());
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
                            _db.SaveSetting(SettingKeys.ReportGroupStageMonths("b", 4), value.ToString());
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
                            _db.SaveSetting(SettingKeys.ReportGroupStageMonths(prefix, 1), value.ToString());
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
                            _db.SaveSetting(SettingKeys.ReportGroupStageMonths(prefix, 2), value.ToString());
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
                            _db.SaveSetting(SettingKeys.ReportGroupStageMonths(prefix, 3), value.ToString());
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
                            _db.SaveSetting(SettingKeys.ReportGroupStageMonths(prefix, 4), value.ToString());
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
                "مکانیک" => "گزارش اتمام تاسیسات مکانیکی",
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
            // برگرداندن همه ماه‌ها به پیش‌فرض، بدون برگشت؛ پس اول تایید می‌خواهد.
            var confirm = MessageBox.Show(
                "همه ماه‌های زمان‌بندی به مقادیر پیش‌فرض برگردد؟\n\nتنظیمات دستی‌ای که برای گروه‌ها و مرحله‌ها گذاشته‌ای، از بین می‌رود.",
                "بازگردانی مقادیر پیش‌فرض", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            // Same table as LoadSchedulingSettings: (months array, prefix, defaults).
            var groups = new (int[] Months, string Prefix, int[] Defaults)[]
            {
                (_groupAMonths, "a", new[] { 0, 2, 6, 10 }),
                (_groupBMonths, "b", new[] { 0, 3, 6, 12, 20 }),
                (_groupCMonths, "c", new[] { 0, 3, 8, 16, 24 }),
                (_groupDMonths, "d", new[] { 0, 3, 8, 16, 30 }),
            };
            foreach (var (months, prefix, defaults) in groups)
            {
                for (int stage = 1; stage < defaults.Length; stage++)
                {
                    months[stage] = defaults[stage];
                    _db.SaveSetting(SettingKeys.ReportGroupStageMonths(prefix, stage), defaults[stage].ToString());
                    OnPropertyChanged($"Group{char.ToUpperInvariant(prefix[0])}Stage{stage}Months");
                }
            }

            // Update UI
            UpdateStageConfigurations();
            ApplyReportFilters();
            StatusMessage = "مقادیر پیش‌فرض بازیابی شد";
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

