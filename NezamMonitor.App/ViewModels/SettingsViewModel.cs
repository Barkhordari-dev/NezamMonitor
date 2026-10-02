using System.Windows.Input;
using Microsoft.Win32;
using NezamMonitor.Core.Theme;
using NezamMonitor.Core.Backup;

namespace NezamMonitor.App.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly Services.ThemeManager _themeManager;
    private readonly string _dbPath;
    private AppTheme _theme;
    private string _statusMessage = "";
    private string _selectedThemeName = "Light";
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

    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public string SelectedThemeName { get => _selectedThemeName; set => SetProperty(ref _selectedThemeName, value); }
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

    public List<string> ThemeNames { get; } = BuiltInThemes.ThemeNameMap.Keys.ToList();
    public List<string> LayoutNames { get; } = new() { "Light", "Spacious", "Professional", "Modern" };
    public List<string> FontFamilies { get; } = new() { "B Nazanin", "Tahoma", "Arial", "Segoe UI", "Times New Roman" };
    public List<string> Alignments { get; } = new() { "Left", "Center", "Right" };
    public List<string> Directions { get; } = new() { "Horizontal", "Vertical" };
    public List<string> FlowDirections { get; } = new() { "RightToLeft", "LeftToRight" };

    public ICommand ApplyThemeCommand { get; }
    public ICommand ApplyColorsCommand { get; }
    public ICommand ApplyFontsCommand { get; }
    public ICommand ApplyDimensionsCommand { get; }
    public ICommand ApplyLayoutCommand { get; }
    public ICommand ApplyDirectionCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand BackupCommand { get; }
    public ICommand RestoreCommand { get; }
    public ICommand FactoryResetCommand { get; }
    public ICommand ImportAndroidCommand { get; }

    public SettingsViewModel(Services.ThemeManager themeManager, string dbPath = "")
    {
        _themeManager = themeManager;
        _dbPath = dbPath;
        _theme = themeManager.LoadTheme();
        themeManager.ApplyTheme(_theme);

        ApplyThemeCommand = new RelayCommand(ApplyTheme);
        ApplyColorsCommand = new RelayCommand(ApplyColors);
        ApplyFontsCommand = new RelayCommand(ApplyFonts);
        ApplyDimensionsCommand = new RelayCommand(ApplyDimensions);
        ApplyLayoutCommand = new RelayCommand(ApplyLayout);
        ApplyDirectionCommand = new RelayCommand(ApplyDirection);
        ResetCommand = new RelayCommand(ResetToDefault);
        BackupCommand = new RelayCommand(ExecuteBackup);
        RestoreCommand = new RelayCommand(ExecuteRestore);
        FactoryResetCommand = new RelayCommand(ExecuteFactoryReset);
        ImportAndroidCommand = new RelayCommand(ExecuteImportAndroid);

        LoadThemeToUI(_theme);
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
    }

    private void ApplyTheme()
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
        StatusMessage = $"\u062a\u0645 \u00ab{BuiltInThemes.ThemeNameMap.GetValueOrDefault(SelectedThemeName, SelectedThemeName)}\u00bb \u0627\u0639\u0645\u0627\u0644 \u0634\u062f \u2713";
    }

    private void ApplyColors()
    {
        _theme.Colors.Primary = NormalizeHex(PrimaryHex);
        _theme.Colors.Secondary = NormalizeHex(SecondaryHex);
        _theme.Colors.Accent = NormalizeHex(AccentHex);
        _theme.Colors.Background = NormalizeHex(BackgroundHex);
        _theme.Colors.Surface = NormalizeHex(SurfaceHex);
        _theme.Colors.Text = NormalizeHex(TextHex);
        _theme.Colors.TextSecondary = NormalizeHex(TextSecondaryHex);
        _theme.Colors.Error = NormalizeHex(ErrorHex);
        _theme.Colors.Success = NormalizeHex(SuccessHex);
        _theme.Colors.Warning = NormalizeHex(WarningHex);
        _theme.Colors.Info = NormalizeHex(InfoHex);
        _theme.Colors.Sidebar = NormalizeHex(SidebarHex);
        _theme.Colors.SidebarText = NormalizeHex(SidebarTextHex);
        _theme.Colors.LogBackground = NormalizeHex(LogBgHex);
        _theme.Colors.SidebarActive = _theme.Colors.Primary;
        _theme.Colors.ButtonPrimary = _theme.Colors.Primary;
        _theme.Name = "Custom";
        _themeManager.SaveTheme(_theme);
        _themeManager.ApplyTheme(_theme);
        StatusMessage = "\u0631\u0646\u06af\u200c\u0647\u0627 \u0627\u0639\u0645\u0627\u0644 \u0634\u062f \u2713";
    }

    private void ApplyFonts()
    {
        _theme.Fonts.FontFamily = FontFamily;
        _theme.Fonts.HeadingFontFamily = FontFamily;
        _theme.Fonts.GeneralFontSize = FontSize;
        _theme.Fonts.HeadingFontSize = HeadingFontSize;
        _theme.Fonts.ButtonFontSize = ButtonFontSize;
        _theme.Fonts.InputFontSize = InputFontSize;
        _theme.Fonts.LabelFontSize = InputFontSize;
        _themeManager.SaveTheme(_theme);
        _themeManager.ApplyTheme(_theme);
        StatusMessage = "\u0641\u0648\u0646\u062a\u200c\u0647\u0627 \u0627\u0639\u0645\u0627\u0644 \u0634\u062f \u2713";
    }

    private void ApplyDimensions()
    {
        _theme.Dimensions.WindowWidth = WindowWidth;
        _theme.Dimensions.WindowHeight = WindowHeight;
        _theme.Dimensions.ButtonWidth = ButtonWidth;
        _theme.Dimensions.ButtonHeight = ButtonHeight;
        _theme.Dimensions.InputHeight = InputHeight;
        _theme.Dimensions.BorderRadius = BorderRadius;
        _theme.Dimensions.ControlSpacing = ControlSpacing;
        _theme.Dimensions.SidebarWidth = SidebarWidth;
        _themeManager.SaveTheme(_theme);
        _themeManager.ApplyTheme(_theme);
        StatusMessage = "\u0627\u0646\u062f\u0627\u0632\u0647\u200c\u0647\u0627 \u0627\u0639\u0645\u0627\u0644 \u0634\u062f \u2713";
    }

    private void ApplyLayout()
    {
        _theme.ButtonLayout.Alignment = ButtonAlignment;
        _theme.ButtonLayout.Direction = ButtonDirection;
        _themeManager.SaveTheme(_theme);
        StatusMessage = "\u0686\u06cc\u062f\u0645\u0627\u0646 \u062f\u06a9\u0645\u0647\u200c\u0647\u0627 \u0627\u0639\u0645\u0627\u0644 \u0634\u062f \u2713";
    }

    private void ApplyDirection()
    {
        _theme.LayoutDirection.TextDirection = TextDirection;
        _theme.LayoutDirection.TableDirection = TableDirection;
        _theme.LayoutDirection.MenuDirection = MenuDirection;
        _theme.LayoutDirection.MainWindowDirection = MainWindowDirection;
        _themeManager.SaveTheme(_theme);
        _themeManager.ApplyTheme(_theme);
        StatusMessage = "\u062c\u0647\u062a\u200c\u062f\u0647\u06cc \u0646\u0645\u0627\u06cc\u0634 \u0627\u0639\u0645\u0627\u0644 \u0634\u062f \u2713";
    }

    private void ResetToDefault()
    {
        var result = System.Windows.MessageBox.Show(
            "\u0622\u06cc\u0627 \u0627\u0632 \u0628\u0627\u0632\u06af\u0631\u062f\u0627\u0646\u06cc \u062a\u0646\u0638\u06cc\u0645\u0627\u062a \u0631\u0627\u0628\u0637 \u06a9\u0627\u0631\u0628\u0631\u06cc \u0628\u0647 \u062d\u0627\u0644\u062a \u0627\u0648\u0644\u06cc\u0647 \u0627\u0637\u0645\u06cc\u0646\u0627\u0646 \u062f\u0627\u0631\u06cc\u062f\u061f",
            "\u062a\u0623\u06cc\u06cc\u062f \u0628\u0627\u0632\u06af\u0631\u062f\u0627\u0646\u06cc",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result != System.Windows.MessageBoxResult.Yes)
        {
            StatusMessage = "\u0627\u0644\u063a\u0627 \u0634\u062f.";
            return;
        }

        _theme = _themeManager.ResetToDefault();
        LoadThemeToUI(_theme);
        _themeManager.ApplyTheme(_theme);
        StatusMessage = "\u062a\u0646\u0638\u06cc\u0645\u0627\u062a \u0628\u0647 \u062d\u0627\u0644\u062a \u0627\u0648\u0644\u06cc\u0647 \u0628\u0627\u0632\u06af\u0631\u062f\u0627\u0646\u06cc \u0634\u062f \u2713";
    }

    private static string NormalizeHex(string hex)
    {
        if (string.IsNullOrEmpty(hex)) return "#FF808080";
        hex = hex.Trim();
        if (!hex.StartsWith("#")) hex = "#" + hex;
        if (hex.Length == 7) hex = "#FF" + hex[1..];
        return hex.ToUpperInvariant();
    }

    private void ExecuteBackup()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Backup files (*.nzbkp)|*.nzbkp",
            DefaultExt = ".nzbkp",
            FileName = $"nezam_backup_{DateTime.Now:yyyyMMdd_HHmmss}.nzbkp"
        };
        if (dialog.ShowDialog() != true) return;

        var result = BackupEngine.CreateBackup(_dbPath, dialog.FileName, "NezamBackup2024");
        if (result.Success)
            StatusMessage = $"✅ پشتیبان ایجاد شد: {result.FileSize / 1024}KB";
        else
            StatusMessage = $"❌ خطا: {string.Join(", ", result.Errors)}";
    }

    private void ExecuteRestore()
    {
        var result1 = System.Windows.MessageBox.Show(
            "⚠️ اطلاعات فعلی برنامه جایگزین خواهد شد.\nآیا ادامه می‌دهید؟",
            "تأیید بازیابی",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (result1 != System.Windows.MessageBoxResult.Yes) return;

        var dialog = new OpenFileDialog
        {
            Filter = "Backup files (*.nzbkp)|*.nzbkp"
        };
        if (dialog.ShowDialog() != true) return;

        var result = BackupEngine.RestoreBackup(dialog.FileName, _dbPath, "NezamBackup2024");
        if (result.Success)
            StatusMessage = "✅ بازیابی با موفقیت انجام شد. برنامه را مجدداً راه‌اندازی کنید.";
        else
            StatusMessage = $"❌ خطا: {string.Join(", ", result.Errors)}";
    }

    private void ExecuteFactoryReset()
    {
        var result1 = System.Windows.MessageBox.Show(
            "⚠️ این عملیات تمام اطلاعات برنامه را پاک می‌کند.\n" +
            "اطلاعات استخراج‌شده، تنظیمات، ویرایش‌ها، تاریخچه و اطلاعات ورود حذف خواهند شد.\n\n" +
            "قابل بازگشت نیست مگر اینکه Backup داشته باشید.\n\n" +
            "آیا مطمئن هستید؟",
            "⚠️ بازگشت به تنظیمات اولیه",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (result1 != System.Windows.MessageBoxResult.Yes) return;

        // Double confirm
        var result2 = System.Windows.MessageBox.Show(
            "آیا واقعاً می‌خواهید تمام اطلاعات را پاک کنید؟\nاین غیرقابل بازگشت است!",
            "تأیید نهایی",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Stop);
        if (result2 != System.Windows.MessageBoxResult.Yes) return;

        var result = BackupEngine.ResetToFactory(_dbPath);
        if (result.Success)
            StatusMessage = "✅ تمام اطلاعات پاک شد. Word Templates حفظ شدند. برنامه را مجدداً راه‌اندازی کنید.";
        else
            StatusMessage = $"❌ خطا: {string.Join(", ", result.Errors)}";
    }

    private void ExecuteImportAndroid()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Android Export (*.nzmdata)|*.nzmdata"
        };
        if (dialog.ShowDialog() != true) return;

        var result = BackupEngine.ImportAndroidChanges(dialog.FileName, _dbPath);
        if (result.Success)
            StatusMessage = $"✅ {result.Message}";
        else
            StatusMessage = $"❌ خطا: {string.Join(", ", result.Errors)}";
    }
}

