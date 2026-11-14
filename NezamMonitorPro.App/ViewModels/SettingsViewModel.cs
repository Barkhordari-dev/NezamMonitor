using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using NezamMonitorPro.App.Services;
using NezamMonitorPro.App.Views;
using NezamMonitorPro.Core.Backup;
using NezamMonitorPro.Core.Theme;

namespace NezamMonitorPro.App.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly Services.ThemeManager _themeManager;
    private readonly string _dbPath;
    private AppTheme _theme;
    private string _statusMessage = "";
    private string _selectedThemeName = "Light";
    private string _selectedButtonStyle = "کلاسیک";
    private string _selectedLayoutName = "Light";

    private string _primaryHex = "#FF3498DB";
    private string _secondaryHex = "#FF2ECC71";
    private string _accentHex = "#FF9B59B6";
    private string _backgroundHex = "#FFF5F5F5";
    private string _surfaceHex = "#FFFFFFFF";
    private string _textHex = "#FF2C3E50";
    private string _textSecondaryHex = "#FF7F8C8D";
    private string _errorHex = "#FFE74C3C";
    private string _successHex = "#FF2ECC71";
    private string _warningHex = "#FFF1C40F";
    private string _infoHex = "#FF3498DB";
    private string _sidebarHex = "#FF2C3E50";
    private string _sidebarTextHex = "#FFFFFFFF";
    private string _logBgHex = "#FF2C3E50";

    private string _fontFamily = "B Nazanin";
    private double _fontSize = 14;
    private double _sidebarFontSize = 14;
    private double _headingFontSize = 20;
    private double _buttonFontSize = 13;
    private double _inputFontSize = 13;

    private double _windowWidth = 1200;
    private double _windowHeight = 750;
    private double _buttonWidth = 180;
    private double _buttonHeight = 40;
    private double _inputHeight = 32;
    private double _borderRadius = 6;
    private double _controlSpacing = 8;
    private double _sidebarWidth = 200;

    private string _buttonAlignment = "Left";
    private string _buttonDirection = "Horizontal";

    private string _textDirection = "RightToLeft";
    private string _tableDirection = "RightToLeft";
    private string _menuDirection = "RightToLeft";
    private string _mainWindowDirection = "RightToLeft";

    // Table text alignment: display value shown in the combo ("وسط" / "راست" /
    // "چپ"), mapped to the stored "Center" / "Right" / "Left" in ApplyTableAlignment.
    private string _tableAlignmentDisplay = "وسط";

    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    private string _statusKind = "info";
    /// <summary>رنگ پیام وضعیت (S-012): info (معمولی)، ok (سبز)، error (قرمز).</summary>
    public string StatusKind { get => _statusKind; set => SetProperty(ref _statusKind, value); }

    /// <summary>پیام وضعیت را با رنگ مناسبش می‌گذارد (سبز برای موفقیت، قرمز برای خطا).</summary>
    private void SetStatus(string message, string kind = "info")
    {
        StatusMessage = message;
        StatusKind = kind;
    }

    private string _updateCheckMessage = "";
    /// <summary>نتیجه «بررسی به‌روزرسانی» که کنار دکمه‌اش در کارت درباره نشان داده می‌شود.</summary>
    public string UpdateCheckMessage { get => _updateCheckMessage; set => SetProperty(ref _updateCheckMessage, value); }

    /// <summary>App version shown on the About card: the assembly version
    /// when the build stamps one, otherwise the known release version.</summary>
    public string AppVersion { get; } = GetAppVersion();

    private static string GetAppVersion()
    {
        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        if (v == null || (v.Major == 1 && v.Minor == 0 && v.Build == 0 && v.Revision == 0)) return "3.0.1";
        return v.ToString(3);
    }
    public string SelectedThemeName { get => _selectedThemeName; set => SetProperty(ref _selectedThemeName, value); }
    /// <summary>Button look display name (Persian), mapped to a ButtonStyleManager key on apply.</summary>
    public string SelectedButtonStyle { get => _selectedButtonStyle; set => SetProperty(ref _selectedButtonStyle, value); }
    public string SelectedLayoutName { get => _selectedLayoutName; set => SetProperty(ref _selectedLayoutName, value); }

    public string PrimaryHex { get => _primaryHex; set => SetProperty(ref _primaryHex, value); }
    public string SecondaryHex { get => _secondaryHex; set => SetProperty(ref _secondaryHex, value); }
    public string AccentHex { get => _accentHex; set => SetProperty(ref _accentHex, value); }
    public string BackgroundHex { get => _backgroundHex; set => SetProperty(ref _backgroundHex, value); }
    public string SurfaceHex { get => _surfaceHex; set => SetProperty(ref _surfaceHex, value); }
    public string TextHex { get => _textHex; set => SetProperty(ref _textHex, value); }
    public string TextSecondaryHex { get => _textSecondaryHex; set => SetProperty(ref _textSecondaryHex, value); }
    public string ErrorHex { get => _errorHex; set => SetProperty(ref _errorHex, value); }
    public string SuccessHex { get => _successHex; set => SetProperty(ref _successHex, value); }
    public string WarningHex { get => _warningHex; set => SetProperty(ref _warningHex, value); }
    public string InfoHex { get => _infoHex; set => SetProperty(ref _infoHex, value); }
    public string SidebarHex { get => _sidebarHex; set => SetProperty(ref _sidebarHex, value); }
    public string SidebarTextHex { get => _sidebarTextHex; set => SetProperty(ref _sidebarTextHex, value); }
    public string LogBgHex { get => _logBgHex; set => SetProperty(ref _logBgHex, value); }

    public string FontFamily { get => _fontFamily; set => SetProperty(ref _fontFamily, value); }
    public double FontSize { get => _fontSize; set => SetProperty(ref _fontSize, value); }
    /// <summary>Sidebar (navigation) font size. The sidebar used a
    /// hard-coded 13.5 and never followed the font settings; it now reads
    /// the SidebarFontSize resource published by ThemeManager.</summary>
    public double SidebarFontSize { get => _sidebarFontSize; set => SetProperty(ref _sidebarFontSize, value); }
    public double HeadingFontSize { get => _headingFontSize; set => SetProperty(ref _headingFontSize, value); }
    public double ButtonFontSize { get => _buttonFontSize; set => SetProperty(ref _buttonFontSize, value); }
    public double InputFontSize { get => _inputFontSize; set => SetProperty(ref _inputFontSize, value); }

    public double WindowWidth { get => _windowWidth; set => SetProperty(ref _windowWidth, value); }
    public double WindowHeight { get => _windowHeight; set => SetProperty(ref _windowHeight, value); }
    public double ButtonWidth { get => _buttonWidth; set => SetProperty(ref _buttonWidth, value); }
    public double ButtonHeight { get => _buttonHeight; set => SetProperty(ref _buttonHeight, value); }
    public double InputHeight { get => _inputHeight; set => SetProperty(ref _inputHeight, value); }
    public double BorderRadius { get => _borderRadius; set => SetProperty(ref _borderRadius, value); }
    public double ControlSpacing { get => _controlSpacing; set => SetProperty(ref _controlSpacing, value); }
    public double SidebarWidth { get => _sidebarWidth; set => SetProperty(ref _sidebarWidth, value); }

    public string ButtonAlignment { get => _buttonAlignment; set => SetProperty(ref _buttonAlignment, value); }
    public string ButtonDirection { get => _buttonDirection; set => SetProperty(ref _buttonDirection, value); }

    public string TextDirection { get => _textDirection; set => SetProperty(ref _textDirection, value); }
    public string TableDirection { get => _tableDirection; set => SetProperty(ref _tableDirection, value); }
    public string MenuDirection { get => _menuDirection; set => SetProperty(ref _menuDirection, value); }
    public string MainWindowDirection { get => _mainWindowDirection; set => SetProperty(ref _mainWindowDirection, value); }

    public string TableAlignmentDisplay { get => _tableAlignmentDisplay; set => SetProperty(ref _tableAlignmentDisplay, value); }

    // Theme names are shown in English, as the user asked.
    public List<string> ThemeNames { get; } = BuiltInThemes.ThemeNameMap.Keys.ToList();
    public List<string> ButtonStyleNames { get; } = Services.ButtonStyleManager.Presets.Select(p => p.DisplayName).ToList();
    public List<string> LayoutNames { get; } = new() { "Light", "Professional", "ProDark" };
    public List<string> TableAlignmentOptions { get; } = new() { "وسط", "راست", "چپ" };
    public List<string> FontFamilies { get; } = new() { "Vazir", "B Nazanin", "Tahoma", "Segoe UI", "Arial" };
    public List<string> Alignments { get; } = new() { "Left", "Center", "Right" };
    public List<string> Directions { get; } = new() { "Horizontal", "Vertical" };
    public List<string> FlowDirections { get; } = new() { "RightToLeft", "LeftToRight" };

    public ICommand ResetCommand { get; }
    public ICommand FactoryResetCommand { get; }
    public ICommand ImportAndroidCommand { get; }
    public ICommand ChangePasswordCommand { get; }
    public ICommand BackupCommand { get; }
    public ICommand RestoreCommand { get; }
    public ICommand ViewErrorLogCommand { get; }
    public ICommand ClearErrorLogCommand { get; }
    public ICommand CheckUpdateCommand { get; }

    public SettingsViewModel(Services.ThemeManager themeManager, string dbPath = "")
    {
        _themeManager = themeManager;
        _dbPath = dbPath;
        _theme = themeManager.LoadTheme();
        themeManager.ApplyTheme(_theme);

        // S-011: دکمه‌های «اعمال» حذف شدند؛ اعمال مستقیم با SelectionChanged
        // در SettingsView.xaml.cs صدا زده می‌شود (ApplyTheme و بقیه public شدند).
        // S-015: ApplyColors/ApplyDimensions حذف شدند (رنگ‌ها از تم می‌آیند).
        ResetCommand = new RelayCommand(ResetToDefault);
        FactoryResetCommand = new RelayCommand(ExecuteFactoryReset);
        ImportAndroidCommand = new RelayCommand(ExecuteImportAndroid);
        ChangePasswordCommand = new RelayCommand(ExecuteChangePassword);
        BackupCommand = new RelayCommand(ExecuteBackup);
        RestoreCommand = new RelayCommand(ExecuteRestore);
        ViewErrorLogCommand = new RelayCommand(ViewErrorLog);
        ClearErrorLogCommand = new RelayCommand(ClearErrorLog);
        CheckUpdateCommand = new AsyncRelayCommand(CheckForUpdateAsync);

        LoadThemeToUI(_theme);
        SelectedButtonStyle = Services.ButtonStyleManager.Find(Services.ButtonStyleManager.Load(Services.DatabaseService.Instance)).DisplayName;
    }

    public void ApplyButtonStyle()
    {
        var preset = Services.ButtonStyleManager.Presets.FirstOrDefault(p => p.DisplayName == SelectedButtonStyle)
            ?? Services.ButtonStyleManager.Presets[0];
        Services.ButtonStyleManager.Apply(preset.Key, Services.DatabaseService.Instance);
        SetStatus($"حالت دکمه‌ها «{preset.DisplayName}» اعمال شد ✓", "ok");
    }

    private void LoadThemeToUI(AppTheme theme)
    {
        SelectedThemeName = theme.Name;
        SelectedLayoutName = theme.Name;
        PrimaryHex = theme.Colors.Primary;
        SecondaryHex = theme.Colors.Secondary;
        AccentHex = theme.Colors.Accent;
        BackgroundHex = theme.Colors.Background;
        SurfaceHex = theme.Colors.Surface;
        TextHex = theme.Colors.Text;
        TextSecondaryHex = theme.Colors.TextSecondary;
        ErrorHex = theme.Colors.Error;
        SuccessHex = theme.Colors.Success;
        WarningHex = theme.Colors.Warning;
        InfoHex = theme.Colors.Info;
        SidebarHex = theme.Colors.Sidebar;
        SidebarTextHex = theme.Colors.SidebarText;
        LogBgHex = theme.Colors.LogBackground;
        FontFamily = theme.Fonts.FontFamily;
        FontSize = theme.Fonts.GeneralFontSize;
        SidebarFontSize = theme.Fonts.SidebarFontSize;
        HeadingFontSize = theme.Fonts.HeadingFontSize;
        ButtonFontSize = theme.Fonts.ButtonFontSize;
        InputFontSize = theme.Fonts.InputFontSize;
        WindowWidth = theme.Dimensions.WindowWidth;
        WindowHeight = theme.Dimensions.WindowHeight;
        ButtonWidth = theme.Dimensions.ButtonWidth;
        ButtonHeight = theme.Dimensions.ButtonHeight;
        InputHeight = theme.Dimensions.InputHeight;
        BorderRadius = theme.Dimensions.BorderRadius;
        ControlSpacing = theme.Dimensions.ControlSpacing;
        SidebarWidth = theme.Dimensions.SidebarWidth;
        ButtonAlignment = theme.ButtonLayout.Alignment;
        ButtonDirection = theme.ButtonLayout.Direction;
        TextDirection = theme.LayoutDirection.TextDirection;
        TableDirection = theme.LayoutDirection.TableDirection;
        MenuDirection = theme.LayoutDirection.MenuDirection;
        MainWindowDirection = theme.LayoutDirection.MainWindowDirection;
        TableAlignmentDisplay = _themeManager.GetTableAlignment() switch
        {
            "Right" => "راست",
            "Left" => "چپ",
            _ => "وسط",
        };
    }

    public void ApplyTheme()
    {
        var builtIn = BuiltInThemes.GetAllThemes().FirstOrDefault(t => t.Name == SelectedThemeName);
        if (builtIn == null) return;
        _theme = builtIn.Clone();
        // حفظ تنظیمات جهت فعلی به جای ریست شدن
        _theme.LayoutDirection.TextDirection = TextDirection;
        _theme.LayoutDirection.TableDirection = TableDirection;
        _theme.LayoutDirection.MenuDirection = MenuDirection;
        _theme.LayoutDirection.MainWindowDirection = MainWindowDirection;
        LoadThemeToUI(_theme);
        _themeManager.SaveTheme(_theme);
        _themeManager.ApplyTheme(_theme);
        SetStatus($"تم «{SelectedThemeName}» اعمال شد ✓", "ok");
    }

    public void ApplyFonts()
    {
        // S-013: اندازه فونت فقط بین ۱۰ تا ۲۴ پوینت قبول است.
        if (!IsFontSizeOk(FontSize) || !IsFontSizeOk(SidebarFontSize) || !IsFontSizeOk(HeadingFontSize)
            || !IsFontSizeOk(ButtonFontSize) || !IsFontSizeOk(InputFontSize))
        {
            SetStatus("اندازه فونت باید بین ۱۰ تا ۲۴ پوینت باشد.", "error");
            return;
        }
        _theme.Fonts.FontFamily = FontFamily;
        _theme.Fonts.HeadingFontFamily = FontFamily;
        _theme.Fonts.GeneralFontSize = FontSize;
        _theme.Fonts.HeadingFontSize = HeadingFontSize;
        _theme.Fonts.ButtonFontSize = ButtonFontSize;
        _theme.Fonts.InputFontSize = InputFontSize;
        _theme.Fonts.SidebarFontSize = SidebarFontSize;
        _theme.Fonts.LabelFontSize = InputFontSize;
        _themeManager.SaveTheme(_theme);
        _themeManager.ApplyTheme(_theme);
        SetStatus("فونت‌ها اعمال شد ✓", "ok");
    }

    private static bool IsFontSizeOk(double value) => value >= 10 && value <= 24;

    public void ApplyTableAlignment()
    {
        var value = TableAlignmentDisplay switch
        {
            "راست" => "Right",
            "چپ" => "Left",
            _ => "Center",
        };
        _themeManager.SaveTableAlignment(value);
        SetStatus("تراز متن جدول‌ها اعمال شد ✓", "ok");
    }



    private void ResetToDefault()
    {
        var result = System.Windows.MessageBox.Show(
            "آیا از بازگردانی تنظیمات رابط کاربری به حالت اولیه اطمینان دارید؟",
            "تایید",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result != System.Windows.MessageBoxResult.Yes)
        {
            SetStatus("لغو شد.");
            return;
        }

        _theme = _themeManager.ResetToDefault();
        LoadThemeToUI(_theme);
        _themeManager.ApplyTheme(_theme);
        SetStatus("تنظیمات به حالت اولیه بازگردانی شد ✓", "ok");
    }

    private void ExecuteFactoryReset()
    {
        var result1 = System.Windows.MessageBox.Show(
            "⚠️ این عملیات تمام اطلاعات برنامه را پاک می‌کند.\n" +
            "اطلاعات استخراج‌شده، تنظیمات، ویرایش‌ها، تاریخچه و اطلاعات ورود حذف خواهند شد.\n\n" +
            "قابل بازگشت نیست مگر اینکه پشتیبان داشته باشید.\n\n" +
            "آیا مطمین هستید؟",
            "پاکسازی کامل اطلاعات",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (result1 != System.Windows.MessageBoxResult.Yes) return;

        // Double confirm
        var result2 = System.Windows.MessageBox.Show(
            "آیا واقعا می‌خواهید تمام اطلاعات را پاک کنید؟\nاین غیرقابل بازگشت است!",
            "تایید",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Stop);
        if (result2 != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            // Use the app's own open connection — a second connection to the
            // same SQLite file deadlocks/fails while the singleton holds it.
            var db = Services.DatabaseService.Instance;
            var cleared = db.ClearAllData();

            // Reset the in-memory theme back to the Pro default as well
            _themeManager.ResetToDefault();
            _themeManager.ApplyTheme();

            // S-014: پیام با واقعیت می‌خواند و برنامه واقعا از اول باز می‌شود.
            System.Windows.MessageBox.Show(
                $"✅ پاکسازی کامل انجام شد — {cleared.Count} جدول خالی شد. " +
                "قالب‌های Word و فهرست مهندسین حفظ شدند. برنامه دوباره باز می‌شود.",
                "پاکسازی کامل اطلاعات",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            App.Restart();
        }
        catch (Exception ex)
        {
            SetStatus($"❌ خطا در پاکسازی: {ex.Message}", "error");
            System.Diagnostics.Debug.WriteLine($"[Settings] FactoryReset failed: {ex}");
        }
    }

    private void ExecuteImportAndroid()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Android Export (*.nzmdata)|*.nzmdata"
        };
        if (dialog.ShowDialog() != true) return;

        // SEC-015: encrypted package — ask for its password first; if the
        // user cancels, the import is cancelled without an error message.
        string? importPassword = null;
        if (BackupEngine.IsPackageEncrypted(dialog.FileName))
        {
            var pwWindow = new BackupPasswordWindow(askConfirm: false)
            {
                Title = "رمز فایل رمزنگاری‌شده"
            };
            if (pwWindow.ShowDialog() != true) return;
            importPassword = pwWindow.Password;
        }

        // ARCH-003: ایمپورت مستقیم روی اتصال مشترک کار می‌کند؛ زیر قفل اتصال انجام می‌شود.
        var db = Services.DatabaseService.Instance;
        ImportResult result;
        lock (db.SyncRoot)
        {
            result = BackupEngine.ImportAndroidChanges(dialog.FileName, db.Connection, importPassword);
        }
        if (result.Success)
            SetStatus($"✅ {result.Message}", "ok");
        else
            SetStatus($"❌ خطا: {string.Join(", ", result.Errors)}", "error");
    }

    private void ExecuteChangePassword()
    {
        var window = new NezamMonitorPro.App.Views.ChangePasswordWindow();
        if (window.ShowDialog() == true)
        {
            // SEC-004: اتصال اصلی با کلید قدیمی می‌ماند؛ برنامه از اول باز می‌شود.
            System.Windows.MessageBox.Show(
                "✅ رمز عوض شد؛ برنامه دوباره باز می‌شود.",
                "تغییر رمز عبور",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            App.Restart();
        }
    }

    /// <summary>M-1: پشتیبان‌گیری از دیتابیس با رمزی که کاربر می‌دهد.</summary>
    private void ExecuteBackup()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "پشتیبان NezamMonitor (*.nzbkp)|*.nzbkp",
            FileName = $"nezam-backup-{DateTime.Now:yyyyMMdd-HHmmss}.nzbkp"
        };
        if (dialog.ShowDialog() != true) return;

        var pwWindow = new BackupPasswordWindow(askConfirm: true);
        if (pwWindow.ShowDialog() != true) return;

        try
        {
            // ARCH-003: بکاپ مستقیم روی اتصال مشترک کار می‌کند؛ زیر قفل اتصال انجام می‌شود.
            var db = Services.DatabaseService.Instance;
            BackupResult result;
            lock (db.SyncRoot)
            {
                result = BackupEngine.CreateBackup(db.Connection, _dbPath, dialog.FileName, pwWindow.Password);
            }
            if (result.Success)
                SetStatus($"✅ پشتیبان‌گیری انجام شد: {dialog.FileName}", "ok");
            else
                SetStatus($"❌ پشتیبان‌گیری نشد: {string.Join("، ", result.Errors)}", "error");
        }
        catch (Exception ex)
        {
            SetStatus($"❌ پشتیبان‌گیری نشد: {ex.Message}", "error");
        }
    }

    /// <summary>M-1: بازیابی از روی فایل پشتیبان؛ اتصال بسته می‌شود و برنامه از اول باز می‌شود.</summary>
    private void ExecuteRestore()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "پشتیبان NezamMonitor (*.nzbkp)|*.nzbkp"
        };
        if (dialog.ShowDialog() != true) return;

        var pwWindow = new BackupPasswordWindow(askConfirm: false);
        if (pwWindow.ShowDialog() != true) return;

        // ARCH-003: بستن اتصال و جایگزینی فایل دیتابیس باید اتمی باشد؛
        // هیچ نخ دیگری هم‌زمان به اتصال مشترک دست نزند. پیام‌ها بیرون از قفل نشان داده می‌شوند.
        BackupResult? restoreResult = null;
        Exception? restoreError = null;
        try
        {
            var db = Services.DatabaseService.Instance;
            lock (db.SyncRoot)
            {
                // اول اتصال بسته می‌شود تا فایل دیتابیس آزاد باشد.
                Services.DatabaseService.Close();
                restoreResult = BackupEngine.RestoreBackup(dialog.FileName, _dbPath, pwWindow.Password);
            }
        }
        catch (Exception ex)
        {
            restoreError = ex;
        }

        if (restoreError != null)
        {
            System.Windows.MessageBox.Show(
                $"❌ بازیابی نشد: {restoreError.Message}\nبرنامه دوباره باز می‌شود.",
                "بازیابی پشتیبان",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        else if (restoreResult != null && restoreResult.Success)
        {
            System.Windows.MessageBox.Show(
                "✅ بازیابی انجام شد؛ برنامه دوباره باز می‌شود.",
                "بازیابی پشتیبان",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        else
        {
            // بازیابی نشد؛ فایل دیتابیس دست‌نخورده مانده و با ری‌استارت دوباره باز می‌شود.
            System.Windows.MessageBox.Show(
                $"❌ بازیابی نشد: {string.Join("، ", restoreResult?.Errors ?? new List<string>())}\nبرنامه دوباره باز می‌شود.",
                "بازیابی پشتیبان",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        App.Restart();
    }

    /// <summary>M-2: فایل لاگ خطا را با برنامه پیش‌فرض ویندوز باز می‌کند.</summary>
    private void ViewErrorLog()
    {
        try
        {
            var path = ErrorLog.LogPath;
            if (!System.IO.File.Exists(path))
            {
                SetStatus("هنوز لاگ خطایی نیست.", "info");
                return;
            }
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            SetStatus($"❌ لاگ خطا باز نشد: {ex.Message}", "error");
        }
    }

    /// <summary>M-2: لاگ خطا را خالی می‌کند.</summary>
    private void ClearErrorLog()
    {
        try
        {
            ErrorLog.Clear();
            SetStatus("لاگ خطا پاک شد.", "ok");
        }
        catch (Exception ex)
        {
            SetStatus($"❌ لاگ خطا پاک نشد: {ex.Message}", "error");
        }
    }

    /// <summary>M-10: آخرین نسخه گیت‌هاب را با نسخه فعلی مقایسه می‌کند.</summary>
    private async Task CheckForUpdateAsync()
    {
        UpdateCheckMessage = "در حال بررسی به‌روزرسانی…";
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("NezamMonitorPro-Windows");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            var json = await http.GetStringAsync(
                "https://api.github.com/repos/Barkhordari-dev/NezamMonitor/releases/latest");
            using var doc = JsonDocument.Parse(json);
            var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            var latest = tag.TrimStart('v', 'V');
            if (Version.TryParse(latest, out var latestVersion)
                && Version.TryParse(AppVersion, out var currentVersion))
            {
                UpdateCheckMessage = latestVersion > currentVersion
                    ? $"نسخه جدید {tag} هست؛ از گیت‌هاب بگیر."
                    : "به‌روز هستی؛ نسخه تازه‌ای نیست.";
            }
            else
            {
                UpdateCheckMessage = string.Equals(latest, AppVersion, StringComparison.OrdinalIgnoreCase)
                    ? "به‌روز هستی؛ نسخه تازه‌ای نیست."
                    : $"نسخه جدید {tag} هست؛ از گیت‌هاب بگیر.";
            }
        }
        catch (HttpRequestException)
        {
            UpdateCheckMessage = "اینترنت وصل نیست؛ بررسی به‌روزرسانی نشد.";
        }
        catch (TaskCanceledException)
        {
            UpdateCheckMessage = "بررسی به‌روزرسانی خیلی طول کشید؛ دوباره تلاش کن.";
        }
        catch (Exception)
        {
            UpdateCheckMessage = "بررسی به‌روزرسانی نشد؛ دوباره تلاش کن.";
        }
    }
}

