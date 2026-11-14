using System.Collections.ObjectModel;
using System.Windows.Input;
using NezamMonitorPro.Core.Data;
using NezamMonitorPro.Core.Models;

namespace NezamMonitorPro.App.ViewModels;

public sealed class CasesViewModel : ViewModelBase
{
    private readonly NezamDatabase _db;
    private string _searchText = "";
    private string _statusMessage = "در حال بارگذاری...";
    private string _detailTitle = "";
    private string _detailEngineers = "";
    private string _detailFees = "";
    private string _detailReports = "";
    private string _detailSpecs = "";
    private CaseDisplayItem? _selectedCase;

    private ObservableCollection<CaseDisplayItem> _cases = new();
    /// <summary>Replaced wholesale on load/filter so the grid rebinds once
    /// instead of receiving one CollectionChanged per row (N UI updates on
    /// every navigation). Backed by SetProperty for the binding.</summary>
    public ObservableCollection<CaseDisplayItem> Cases { get => _cases; set => SetProperty(ref _cases, value); }
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value)) return;
            // PERF: پیش‌تر هر کلید مستقیم FilterCases را صدا می‌زد — یعنی
            // جایگذاری کامل ItemsSource و اندازه‌گیری دوبارهٔ هر ۳۴ ستون
            // Auto روی نخ UI، با هر حرف. حالا ۳۰۰ میلی‌ثانیه صبر می‌کنیم تا
            // کاربر مکث کند و فقط یک بار فیلتر می‌شود.
            RestartSearchDebounce();
        }
    }
    // L-016 / DUP-003: وضعیت فیلتر (پیش‌نویس + اعمال‌شده) در سرویس مشترک
    // FilterState نگه داشته می‌شود؛ همان ماشین وضعیت در Fees و FollowUp هم
    // استفاده می‌شود. کنترل‌ها فقط Draft را عوض می‌کنند؛ جدول تا وقتی
    // ApplyFilters صدا زده نشود فیلتر نمی‌شود.
    public Services.FilterState Filters { get; } = new();
    /// <summary>True وقتی فیلتری انتخاب شده ولی «اعمال» هنوز زده نشده.</summary>
    public bool HasPendingFilters => Filters.HasPendingChanges;
    public string DraftGroup { get => Filters.GetDraft("Group"); set { Filters.SetDraft("Group", value); OnPropertyChanged(nameof(DraftGroup)); OnPropertyChanged(nameof(HasPendingFilters)); } }
    public string DraftDiscipline { get => Filters.GetDraft("Discipline"); set { Filters.SetDraft("Discipline", value); OnPropertyChanged(nameof(DraftDiscipline)); OnPropertyChanged(nameof(HasPendingFilters)); } }
    public string DraftOwner { get => Filters.GetDraft("Owner"); set { Filters.SetDraft("Owner", value); OnPropertyChanged(nameof(DraftOwner)); OnPropertyChanged(nameof(HasPendingFilters)); } }
    public string DraftOffice { get => Filters.GetDraft("Office"); set { Filters.SetDraft("Office", value); OnPropertyChanged(nameof(DraftOffice)); OnPropertyChanged(nameof(HasPendingFilters)); } }
    public bool HasActiveFilters => Filters.HasActiveFilters;
    /// <summary>گزینه‌های فیلتر گروه/رشته/مالک/دفتر (XAML به این‌ها بایند است؛
    /// RebuildFilterOptions آن‌ها را از داده‌ها می‌سازد؛ «همه» همیشه اول است).</summary>
    public ObservableCollection<string> GroupFilters { get; } = new() { "همه" };
    public ObservableCollection<string> DisciplineFilters { get; } = new() { "همه" };
    public ObservableCollection<string> OwnerFilters { get; } = new() { "همه" };
    public ObservableCollection<string> OfficeFilters { get; } = new() { "همه" };
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public string DetailTitle { get => _detailTitle; set => SetProperty(ref _detailTitle, value); }
    public string DetailEngineers { get => _detailEngineers; set => SetProperty(ref _detailEngineers, value); }
    public string DetailFees { get => _detailFees; set => SetProperty(ref _detailFees, value); }
    public string DetailReports { get => _detailReports; set => SetProperty(ref _detailReports, value); }
    public string DetailSpecs { get => _detailSpecs; set => SetProperty(ref _detailSpecs, value); }
    public CaseDisplayItem? SelectedCase { get => _selectedCase; set => SetProperty(ref _selectedCase, value); }
    public bool HasDetail => SelectedCase != null;
    public ICommand RefreshCommand { get; }
    public ICommand CloseDetailCommand { get; }
    public ICommand ApplyFiltersCommand { get; }
    public ICommand ClearFiltersCommand { get; }

    private List<CaseDisplayItem> _allCases = new();

    /// <summary>تایمر ۳۰۰ میلی‌ثانیه‌ای جستجو (PERF): فیلتر فقط وقتی اجرا
    /// می‌شود که کاربر مکث کند. تایمر بعد از هر شلیک خودش را خاموش می‌کند،
    /// پس وقتی در حال اجرا نیست هیچ ارجاعی نگه نمی‌دارد.</summary>
    private readonly System.Windows.Threading.DispatcherTimer _searchDebounceTimer;

    private void RestartSearchDebounce()
    {
        _searchDebounceTimer.Stop();
        _searchDebounceTimer.Start();
    }

    /// <summary>Snapshot the grid was loaded from. The view is cached now,
    /// so OnNavigatedTo compares it with the active snapshot and reloads
    /// only when new data was extracted in the meantime.</summary>
    private int _loadedSnapshotId;

    public CasesViewModel(NezamDatabase db)
    {
        _db = db;
        Filters.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasActiveFilters));
            OnPropertyChanged(nameof(HasPendingFilters));
        };
        RefreshCommand = new RelayCommand(LoadCases);
        CloseDetailCommand = new RelayCommand(CloseDetail);
        ApplyFiltersCommand = new RelayCommand(ApplyFilters);
        ClearFiltersCommand = new RelayCommand(ClearFilters);
        _searchDebounceTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _searchDebounceTimer.Tick += (_, _) =>
        {
            _searchDebounceTimer.Stop();
            FilterCases();
        };
        LoadCases();
    }

    private void LoadCases()
    {
        try
        {
            Cases.Clear();
            var snapshotId = _db.GetActiveSnapshotId();
            if (snapshotId == 0)
            {
                _loadedSnapshotId = 0;
                StatusMessage = "هنوز داده‌ای ذخیره نشده است.";
                return;
            }
            // Light load: the grid shows only Case + Specification columns;
            // Engineers/Fees/Reports are loaded on demand in ShowCaseDetails.
            var rawCases = _db.LoadCasesLight(snapshotId);
            _allCases = rawCases.Select((c, i) => new CaseDisplayItem(c) { RowNumber = i + 1 }).ToList();
            RebuildFilterOptions();
            Cases = new ObservableCollection<CaseDisplayItem>(_allCases);
            _loadedSnapshotId = snapshotId;
            StatusMessage = $"{Cases.Count} پرونده بارگذاری شد";
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطا: {ex.Message}";
        }
    }

    /// <summary>Called on every navigation to the (now cached) Cases
    /// view: a single cheap query; reloads (light load) only when the active
    /// snapshot changed since the grid was loaded, so returning to the menu
    /// is instant while newly extracted data still shows up.</summary>
    public void RefreshIfSnapshotChanged()
    {
        if (_db.GetActiveSnapshotId() != _loadedSnapshotId) LoadCases();
    }

    public void ShowCaseDetails(CaseDisplayItem item)
    {
        // Children were not loaded by the light grid load; fetch this one
        // case's Engineers/Fees/Reports (3 indexed queries) before building
        // the detail strings.
        _db.LoadCaseChildren(item.RawCase);
        SelectedCase = item;
        DetailTitle = $"{item.CaseNumber} | {item.Owner}";

        // Engineers
        var engineers = item.RawCase.Engineers;
        if (engineers.Count > 0)
        {
            var lines = engineers.Select(e =>
            {
                var role = string.IsNullOrEmpty(e.Role) ? "" : $" ({e.Role})";
                return $"  {e.Discipline}: {e.Name}{role}";
            });
            DetailEngineers = string.Join("\n", lines);
        }
        else
            DetailEngineers = "  (ندارد)";

        // Fees — نگاشت نوع خدمت مثل منو حق‌الزحمه (به‌جای حروف n/b/ho/t/pn)
        var fees = item.RawCase.Fees;
        if (fees.Count > 0)
        {
            var stage0 = fees.Where(f => f.Stage?.Trim() == "0").ToList();
            var amountMap = new Dictionary<Fee, long>();
            foreach (var f in stage0) amountMap[f] = ParseFeeAmount(f.Amount);
            var ordered0 = stage0.OrderByDescending(f => amountMap[f]).ToList();
            string[] order0 = { "حسن انجام کار", "نظارت سهم سازمان", "مالیات نظارت سهم سازمان", "بیمه" };
            var serviceForFee = new Dictionary<Fee, string>();
            for (int i = 0; i < ordered0.Count; i++)
            {
                string svc = i < order0.Length ? order0[i] : order0[^1];
                serviceForFee[ordered0[i]] = svc;
            }
            var lines = fees.Select(f =>
            {
                string svc;
                if (f.Stage?.Trim() != "0")
                    svc = "نظارت";
                else if (!serviceForFee.TryGetValue(f, out svc!))
                    svc = "بیمه";
                var pay = string.IsNullOrEmpty(f.PayStatus) || f.PayStatus == "_" ? "" : $" [{f.PayStatus}]";
                long amount = ParseFeeAmount(f.Amount);
                return $"  {svc} مرحله {f.Stage}: {amount:N0} ریال{pay}";
            });
            DetailFees = string.Join("\n", lines);
        }
        else
            DetailFees = "  (ندارد)";

        // Reports
        var reports = item.RawCase.Reports;
        if (reports.Count > 0)
        {
            var lines = reports.Take(15).Select(r =>
                // TXT-002: خود Stage عنوان کامل «مرحله اول: …» است؛ پیشوند
                // «مرحله » اضافه می‌شد و «مرحله مرحله اول» درمی‌آمد.
                $"  {r.ReportType} {r.Stage} | {r.Engineer} | {r.VisitDate}");
            var text = string.Join("\n", lines);
            if (reports.Count > 15)
                text += $"\n  ... و {reports.Count - 15} گزارش دیگر";
            DetailReports = text;
        }
        else
            DetailReports = "  (ندارد)";

        // Specs
        var s = item.RawCase.Specification;
        if (s != null)
        {
            var lines = new List<string>();
            if (!string.IsNullOrEmpty(s.UsageType)) lines.Add($"  نوع کاربری: {s.UsageType}");
            if (!string.IsNullOrEmpty(s.BuildingGroup)) lines.Add($"  گروه ساختمانی: {s.BuildingGroup}");
            if (!string.IsNullOrEmpty(s.RenovationCode)) lines.Add($"  کد نوسازی: {s.RenovationCode}");
            if (!string.IsNullOrEmpty(s.PlanInstructionNo)) lines.Add($"  شماره دستور نقشه: {s.PlanInstructionNo}");
            if (!string.IsNullOrEmpty(s.PlanInstructionType)) lines.Add($"  نوع دستور نقشه: {s.PlanInstructionType}");
            if (!string.IsNullOrEmpty(s.PlanInstructionDate)) lines.Add($"  تاریخ دستور نقشه: {s.PlanInstructionDate}");
            if (!string.IsNullOrEmpty(s.LandArea)) lines.Add($"  مساحت زمین: {s.LandArea}");
            if (!string.IsNullOrEmpty(s.ParafArea)) lines.Add($"  متراژ پاراف: {s.ParafArea}");
            if (!string.IsNullOrEmpty(s.CapacityArea)) lines.Add($"  متراژ کسر ظرفیت: {s.CapacityArea}");
            if (!string.IsNullOrEmpty(s.StructureType)) lines.Add($"  نوع سازه: {s.StructureType}");
            if (!string.IsNullOrEmpty(s.BlockTitle)) lines.Add($"  عنوان بلوک: {s.BlockTitle}");
            if (!string.IsNullOrEmpty(s.BlockCount)) lines.Add($"  تعداد بلوک: {s.BlockCount}");
            if (!string.IsNullOrEmpty(s.Floors)) lines.Add($"  طبقات: {s.Floors}");
            if (!string.IsNullOrEmpty(s.Units)) lines.Add($"  واحدها: {s.Units}");
            if (!string.IsNullOrEmpty(s.Issuer)) lines.Add($"  صادرکننده: {s.Issuer}");
            if (!string.IsNullOrEmpty(s.PermitNumber)) lines.Add($"  شماره پروانه: {s.PermitNumber}");
            if (!string.IsNullOrEmpty(s.PermitDate)) lines.Add($"  تاریخ صدور: {s.PermitDate}");
            if (!string.IsNullOrEmpty(s.ReleaseDate)) lines.Add($"  تاریخ ترخیص: {s.ReleaseDate}");
            if (!string.IsNullOrEmpty(s.PlanZone)) lines.Add($"  محدوده طرح: {s.PlanZone}");
            if (!string.IsNullOrEmpty(s.Address)) lines.Add($"  آدرس: {s.Address}");
            DetailSpecs = lines.Count > 0 ? string.Join("\n", lines) : "  (بدون فیلد)";
        }
        else
            DetailSpecs = "  (ندارد)";

        OnPropertyChanged(nameof(HasDetail));
    }

    private void CloseDetail()
    {
        SelectedCase = null;
        DetailTitle = "";
        DetailEngineers = "";
        DetailFees = "";
        DetailReports = "";
        DetailSpecs = "";
        OnPropertyChanged(nameof(HasDetail));
    }

    private static long ParseFeeAmount(string amount)
    {
        if (string.IsNullOrWhiteSpace(amount)) return 0;
        var cleaned = amount.Replace("ریال", "").Replace(",", "").Replace(" ", "").Trim();
        cleaned = cleaned.Replace("۰", "0").Replace("۱", "1").Replace("۲", "2").Replace("۳", "3").Replace("۴", "4").Replace("۵", "5").Replace("۶", "6").Replace("۷", "7").Replace("۸", "8").Replace("۹", "9").Replace("٠", "0").Replace("١", "1").Replace("٢", "2").Replace("٣", "3").Replace("٤", "4").Replace("٥", "5").Replace("٦", "6").Replace("٧", "7").Replace("٨", "8").Replace("٩", "9");
        return long.TryParse(cleaned, out var r) ? r : 0;
    }

    private void FilterCases()
    {
        // تایمر معلق را خاموش کن تا دوبار اجرا نشود (مثلا «اعمال فیلتر»
        // بلافاصله بعد از تایپ).
        _searchDebounceTimer.Stop();
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allCases
            : _allCases.Where(c =>
                c.CaseNumber.Contains(SearchText) ||
                c.Owner.Contains(SearchText) ||
                c.Serial.Contains(SearchText) ||
                c.Id.ToString().Contains(SearchText)).ToList();
        // P4: applied selection filters (draft values only take effect via Apply).
        filtered = FilterHelper.ApplyFilters(filtered,
            (c => c.BuildingGroup, Filters.GetApplied("Group")),
            (c => c.Responsibility, Filters.GetApplied("Discipline")),
            (c => c.Owner, Filters.GetApplied("Owner")),
            (c => c.Office, Filters.GetApplied("Office"))).ToList();
        // PERF: اگر نتیجه دقیقاً همان چیزی است که الان نشان داده می‌شود،
        // جایگذاری دوبارهٔ ItemsSource (و اندازه‌گیری دوبارهٔ ۳۴ ستون Auto)
        // بیهوده است. پیام وضعیت را اول به‌روز می‌کنیم تا متن آن دقیقاً
        // مثل قبل باشد، بعد فقط جایگذاری را رد می‌کنیم.
        // M-9: وقتی جستجو/فیلتر چیزی پیدا نکرد، پیام روشن است.
        StatusMessage = filtered.Count == 0 && _allCases.Count > 0 ? "چیزی پیدا نشد" : $"{filtered.Count} پرونده";
        if (_cases.Count == filtered.Count && _cases.SequenceEqual(filtered))
            return;
        Cases = new ObservableCollection<CaseDisplayItem>(filtered);
        OnPropertyChanged(nameof(HasActiveFilters));
    }

    /// <summary>Commits the draft filter selections (P4).</summary>
    private void ApplyFilters()
    {
        Filters.Apply();
        FilterCases();
    }

    private void ClearFilters()
    {
        Filters.Clear(("Group", "همه"), ("Discipline", "همه"), ("Owner", "همه"), ("Office", "همه"));
        OnPropertyChanged(nameof(DraftGroup));
        OnPropertyChanged(nameof(DraftDiscipline));
        OnPropertyChanged(nameof(DraftOwner));
        OnPropertyChanged(nameof(DraftOffice));
        ApplyFilters();
    }

    private void RebuildFilterOptions()
    {
        GroupFilters.Clear();
        foreach (var v in FilterHelper.ExtractDistinctValues(_allCases, c => c.BuildingGroup)) GroupFilters.Add(v);
        DisciplineFilters.Clear();
        foreach (var v in FilterHelper.ExtractDistinctValues(_allCases, c => c.Responsibility)) DisciplineFilters.Add(v);
        OwnerFilters.Clear();
        foreach (var v in FilterHelper.ExtractDistinctValues(_allCases, c => c.Owner)) OwnerFilters.Add(v);
        OfficeFilters.Clear();
        foreach (var v in FilterHelper.ExtractDistinctValues(_allCases, c => c.Office)) OfficeFilters.Add(v);
    }
}

/// <summary>Flattened display item that exposes Case + Specification fields for DataGrid binding.</summary>
public sealed class CaseDisplayItem : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    private void On(string? n) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(n));
    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set { _isSelected = value; On(nameof(IsSelected)); } }
    public Case RawCase { get; }
    public int RowNumber { get; set; }

    // Case fields
    public long Id => RawCase.Id;
    public string CaseNumber => RawCase.CaseNumber;
    public string Serial => RawCase.Serial;
    public string Owner => RawCase.Owner;
    public string OwnerMobile => RawCase.OwnerMobile;
    public string Responsibility => RawCase.Responsibility;
    public string CapacityDate => RawCase.CapacityDate;
    public string Office => RawCase.Office;
    public string OwnerFather => RawCase.OwnerFather;
    public string OwnerNationalCode => RawCase.OwnerNationalCode;
    public string OwnerAddress => RawCase.OwnerAddress;
    public string OwnerZip => RawCase.OwnerZip;
    public string OwnerTel => RawCase.OwnerTel;
    public string OwnerBirthLoc => RawCase.OwnerBirthLoc;

    // Specification fields (flattened)
    public string BuildingGroup => RawCase.Specification?.BuildingGroup ?? "";
    public string RenovationCode => RawCase.Specification?.RenovationCode ?? "";
    public string PlanInstructionNo => RawCase.Specification?.PlanInstructionNo ?? "";
    public string PlanInstructionType => RawCase.Specification?.PlanInstructionType ?? "";
    public string PlanInstructionDate => RawCase.Specification?.PlanInstructionDate ?? "";
    public string LandArea => RawCase.Specification?.LandArea ?? "";
    public string ParafArea => RawCase.Specification?.ParafArea ?? "";
    public string CapacityArea => RawCase.Specification?.CapacityArea ?? "";
    public string StructureType => RawCase.Specification?.StructureType ?? "";
    public string BlockTitle => RawCase.Specification?.BlockTitle ?? "";
    public string BlockCount => RawCase.Specification?.BlockCount ?? "";
    public string Floors => RawCase.Specification?.Floors ?? "";
    public string Units => RawCase.Specification?.Units ?? "";
    public string Issuer => RawCase.Specification?.Issuer ?? "";
    public string PermitNumber => RawCase.Specification?.PermitNumber ?? "";
    public string PermitDate => RawCase.Specification?.PermitDate ?? "";
    public string ReleaseDate => RawCase.Specification?.ReleaseDate ?? "";
    public string PlanZone => RawCase.Specification?.PlanZone ?? "";
    public string Address => RawCase.Specification?.Address ?? "";
    public string UsageType => RawCase.Specification?.UsageType ?? "";

    public CaseDisplayItem(Case c) => RawCase = c;
}
