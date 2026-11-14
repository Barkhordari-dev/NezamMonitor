using System.Globalization;
using System.Windows.Input;
using NezamMonitorPro.Core.Data;
using NezamMonitorPro.Core.Models;

namespace NezamMonitorPro.App.ViewModels;

public sealed class DashboardViewModel : ViewModelBase
{
    private readonly NezamDatabase _db;

    private int _totalCases;
    private int _totalEngineers;
    private int _totalFees;
    private int _totalReports;
    private string _connectionStatus = "غیر متصل";
    private string _lastUpdate = "هنوز انجام نشده";
    private string _welcomeMessage = "سلام، خوش آمدید 👋";
    /// <summary>Personal greeting with the user's name (generic until the first update).</summary>
    public string WelcomeMessage { get => _welcomeMessage; set => SetProperty(ref _welcomeMessage, value); }

    /// <summary>App version shown on the hero badge (same source as Settings → About).</summary>
    public string AppVersion { get; } = GetAppVersion();

    private static string GetAppVersion()
    {
        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        if (v == null || (v.Major == 1 && v.Minor == 0 && v.Build == 0 && v.Revision == 0)) return "3.0.1";
        return v.ToString(3);
    }

    private int _casesWithSpecs;
    private int _casesWithoutSpecs;
    private int _casesWithEngineers;
    private int _casesWithoutEngineers;
    private int _casesWithFees;
    private int _casesWithoutFees;
    private int _casesWithReports;
    private int _casesWithoutReports;


    public int TotalCases { get => _totalCases; set => SetProperty(ref _totalCases, value); }
    public int TotalEngineers { get => _totalEngineers; set => SetProperty(ref _totalEngineers, value); }
    public int TotalFees { get => _totalFees; set => SetProperty(ref _totalFees, value); }
    public int TotalReports { get => _totalReports; set => SetProperty(ref _totalReports, value); }
    public string ConnectionStatus { get => _connectionStatus; set => SetProperty(ref _connectionStatus, value); }
    public string LastUpdate { get => _lastUpdate; set => SetProperty(ref _lastUpdate, value); }

    public int CasesWithSpecs { get => _casesWithSpecs; set => SetProperty(ref _casesWithSpecs, value); }
    public int CasesWithoutSpecs { get => _casesWithoutSpecs; set => SetProperty(ref _casesWithoutSpecs, value); }
    public int CasesWithEngineers { get => _casesWithEngineers; set => SetProperty(ref _casesWithEngineers, value); }
    public int CasesWithoutEngineers { get => _casesWithoutEngineers; set => SetProperty(ref _casesWithoutEngineers, value); }
    public int CasesWithFees { get => _casesWithFees; set => SetProperty(ref _casesWithFees, value); }
    public int CasesWithoutFees { get => _casesWithoutFees; set => SetProperty(ref _casesWithoutFees, value); }
    public int CasesWithReports { get => _casesWithReports; set => SetProperty(ref _casesWithReports, value); }
    public int CasesWithoutReports { get => _casesWithoutReports; set => SetProperty(ref _casesWithoutReports, value); }

    public ICommand RefreshCommand { get; }

    public DashboardViewModel(NezamDatabase db)
    {
        _db = db;
        RefreshCommand = new RelayCommand(() => LoadDashboard());
        // No load here: NavigationService calls OnNavigatedTo() (DashboardView)
        // on every navigation, including the first one, which runs
        // LoadDashboard. Loading here too meant every dashboard visit loaded
        // the whole database twice.
    }

    private void LoadDashboard()
    {
        try
        {
            // Personal welcome: the name is known after the first update.
            // Set it first so it shows even before any data exists.
            // A display-name override from the hidden menu wins over the API name.
            var displayOverride = Services.UiCustomizationService.GetDisplayNameOverride(_db);
            var userName = string.IsNullOrWhiteSpace(displayOverride)
                ? _db.GetSetting(SettingKeys.ApiUserName)
                : displayOverride;
            WelcomeMessage = string.IsNullOrWhiteSpace(userName)
                ? "سلام، خوش آمدید 👋"
                : $"سلام {userName}، خوش آمدی 👋";

            var snapshotId = _db.GetActiveSnapshotId();
            if (snapshotId == 0)
            {
                ConnectionStatus = "غیر متصل";
                LastUpdate = "هنوز انجام نشده";
                return;
            }

            var cases = _db.LoadCases(snapshotId);

            TotalCases = cases.Count;
            TotalEngineers = cases.Sum(c => c.Engineers.Count);
            TotalFees = cases.Sum(c => c.Fees.Count);
            TotalReports = cases.Sum(c => c.Reports.Count);
            ConnectionStatus = "متصل";
            // TXT-001: تاریخ واقعی آخرین به‌روزرسانی از اسنپ‌شات فعال
            // (CreatedAt به‌صورت ISO میلادی ذخیره می‌شود؛ به شمسی برگردانده می‌شود).
            LastUpdate = LastUpdateFromSnapshot(snapshotId);

            CasesWithSpecs = cases.Count(c => c.Specification != null);
            CasesWithoutSpecs = TotalCases - CasesWithSpecs;
            CasesWithEngineers = cases.Count(c => c.Engineers.Count > 0);
            CasesWithoutEngineers = TotalCases - CasesWithEngineers;
            CasesWithFees = cases.Count(c => c.Fees.Count > 0);
            CasesWithoutFees = TotalCases - CasesWithFees;
            CasesWithReports = cases.Count(c => c.Reports.Count > 0);
            CasesWithoutReports = TotalCases - CasesWithReports;
        }
        catch (Exception ex)
        {
            // The old code swallowed this silently, which is why the dashboard
            // appeared "empty" with no clue why. Surface it instead.
            // TXT-010: متن خام استثنا به کاربر نشان داده نمی‌شود؛ جزییات فقط در لاگ.
            ConnectionStatus = "خطا در بارگذاری";
            LastUpdate = "خطا در بارگذاری";
            NezamMonitorPro.App.Services.ErrorLog.Write(ex, "DashboardViewModel.LoadDashboard");
            System.Diagnostics.Debug.WriteLine($"[Dashboard] LoadDashboard failed: {ex}");
        }
    }

    /// <summary>تاریخ و ساعت آخرین به‌روزرسانی از اسنپ‌شات فعال، به شمسی
    /// (مثلا «۱۴۰۴/۰۸/۲۲ — ۰۹:۴۳»)؛ اگر نباشد «هنوز انجام نشده».</summary>
    private string LastUpdateFromSnapshot(int snapshotId)
    {
        try
        {
            var snap = _db.GetSnapshots().FirstOrDefault(s => s.Id == snapshotId);
            if (string.IsNullOrWhiteSpace(snap.CreatedAt)) return "هنوز انجام نشده";
            var shamsi = NezamMonitorPro.Core.PersianDateHelper.GregoryStringToShamsi(snap.CreatedAt);
            if (string.IsNullOrWhiteSpace(shamsi)) return "هنوز انجام نشده";
            // «۱۴۰۴/۰۸/۲۲ ۰۹:۴۳» → «۱۴۰۴/۰۸/۲۲ — ۰۹:۴۳»
            var withDigits = NezamMonitorPro.Core.DigitNormalizer.ToPersianDigits(shamsi);
            return withDigits.Replace(" ", " — ");
        }
        catch
        {
            return "هنوز انجام نشده";
        }
    }

}
