using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using NezamMonitorPro.Core.Theme;
using NezamMonitorPro.Core.Data;

namespace NezamMonitorPro.App.Services;

/// <summary>
/// Manages theme application, persistence, and reset.
/// </summary>
public sealed class ThemeManager
{
    private readonly NezamDatabase _db;
    private AppTheme _currentTheme;
    private const string DbPrefix = "theme_";

    public AppTheme CurrentTheme => _currentTheme;

    public ThemeManager(NezamDatabase db)
    {
        _db = db;
        _currentTheme = BuiltInThemes.Pro;
    }

    public AppTheme LoadTheme()
    {
        var settings = _db.LoadSettings();
        var theme = BuiltInThemes.Pro;

        if (settings.TryGetValue($"{DbPrefix}Name", out var name))
        {
            var allThemes = BuiltInThemes.GetAllThemes();
            var match = allThemes.FirstOrDefault(t => t.Name == name);
            if (match != null) theme = match.Clone();
        }

        if (settings.TryGetValue($"{DbPrefix}Colors", out var colorJson))
        {
            try { theme.Colors = ThemeColors.FromDictionary(DeserializeDict(colorJson)); } catch { }
        }
        if (settings.TryGetValue($"{DbPrefix}Fonts", out var fontJson))
        {
            try { theme.Fonts = ThemeFonts.FromDictionary(DeserializeDict(fontJson)); } catch { }
        }
        if (settings.TryGetValue($"{DbPrefix}Dimensions", out var dimJson))
        {
            try { theme.Dimensions = ThemeDimensions.FromDictionary(DeserializeDict(dimJson)); } catch { }
        }
        if (settings.TryGetValue($"{DbPrefix}ButtonLayout", out var blJson))
        {
            try { theme.ButtonLayout = ButtonLayout.FromDictionary(DeserializeDict(blJson)); } catch { }
        }
        if (settings.TryGetValue($"{DbPrefix}LayoutDirection", out var ldJson))
        {
            try { theme.LayoutDirection = LayoutDirection.FromDictionary(DeserializeDict(ldJson)); } catch { }
        }

        _currentTheme = theme;
        return theme;
    }

    public void SaveTheme(AppTheme theme)
    {
        _currentTheme = theme;
        _db.SaveSetting($"{DbPrefix}Name", theme.Name);
        _db.SaveSetting($"{DbPrefix}Colors", SerializeDict(theme.Colors.ToDictionary()));
        _db.SaveSetting($"{DbPrefix}Fonts", SerializeDict(theme.Fonts.ToDictionary()));
        _db.SaveSetting($"{DbPrefix}Dimensions", SerializeDict(theme.Dimensions.ToDictionary()));
        _db.SaveSetting($"{DbPrefix}ButtonLayout", SerializeDict(theme.ButtonLayout.ToDictionary()));
        _db.SaveSetting($"{DbPrefix}LayoutDirection", SerializeDict(theme.LayoutDirection.ToDictionary()));
    }

    public AppTheme ResetToDefault()
    {
        var defaultTheme = BuiltInThemes.Light;
        SaveTheme(defaultTheme);
        return defaultTheme;
    }

    public void ApplyTheme(AppTheme? theme = null)
    {
        theme ??= _currentTheme;

        // Derive the soft active-sidebar tint from the current colours so it
        // always matches the theme, including themes restored from the
        // Settings table that were saved before this colour existed.
        try { theme.Colors.DeriveSidebarActiveSoft(); } catch { }
        try { theme.Colors.DeriveTableTints(); } catch { }

        _currentTheme = theme;

        var app = Application.Current;
        if (app == null) return;

        // Colors
        app.Resources["PrimaryBrush"] = new SolidColorBrush(HexToColor(theme.Colors.Primary));
        app.Resources["SecondaryBrush"] = new SolidColorBrush(HexToColor(theme.Colors.Secondary));
        app.Resources["AccentBrush"] = new SolidColorBrush(HexToColor(theme.Colors.Accent));
        app.Resources["BackgroundBrush"] = new SolidColorBrush(HexToColor(theme.Colors.Background));
        app.Resources["SurfaceBrush"] = new SolidColorBrush(HexToColor(theme.Colors.Surface));
        app.Resources["PanelBrush"] = new SolidColorBrush(HexToColor(theme.Colors.Panel));
        app.Resources["CardBrush"] = new SolidColorBrush(HexToColor(theme.Colors.Card));
        app.Resources["ButtonPrimaryBrush"] = new SolidColorBrush(HexToColor(theme.Colors.ButtonPrimary));
        app.Resources["ButtonHoverBrush"] = new SolidColorBrush(HexToColor(theme.Colors.ButtonPrimaryHover));
        app.Resources["ButtonPressedBrush"] = new SolidColorBrush(HexToColor(theme.Colors.ButtonPrimaryPressed));
        app.Resources["ButtonDisabledBrush"] = new SolidColorBrush(HexToColor(theme.Colors.ButtonDisabled));
        app.Resources["InputBackgroundBrush"] = new SolidColorBrush(HexToColor(theme.Colors.InputBackground));
        app.Resources["InputBorderBrush"] = new SolidColorBrush(HexToColor(theme.Colors.InputBorder));
        app.Resources["TextBrush"] = new SolidColorBrush(HexToColor(theme.Colors.Text));
        app.Resources["TextSecondaryBrush"] = new SolidColorBrush(HexToColor(theme.Colors.TextSecondary));
        app.Resources["BorderBrush"] = new SolidColorBrush(HexToColor(theme.Colors.Border));
        app.Resources["SuccessBrush"] = new SolidColorBrush(HexToColor(theme.Colors.Success));
        app.Resources["WarningBrush"] = new SolidColorBrush(HexToColor(theme.Colors.Warning));
        app.Resources["ErrorBrush"] = new SolidColorBrush(HexToColor(theme.Colors.Error));
        app.Resources["InfoBrush"] = new SolidColorBrush(HexToColor(theme.Colors.Info));
        app.Resources["SidebarBrush"] = new SolidColorBrush(HexToColor(theme.Colors.Sidebar));
        app.Resources["SidebarTextBrush"] = new SolidColorBrush(HexToColor(theme.Colors.SidebarText));
        app.Resources["SidebarActiveBrush"] = new SolidColorBrush(HexToColor(theme.Colors.SidebarActive));
        app.Resources["SidebarActiveSoftBrush"] = new SolidColorBrush(HexToColor(theme.Colors.SidebarActiveSoft));
        app.Resources["SidebarHoverBrush"] = new SolidColorBrush(HexToColor(theme.Colors.SidebarHover));
        app.Resources["HoverOverlayBrush"] = new SolidColorBrush(HexToColor(theme.Colors.HoverOverlay));
        app.Resources["ZebraRowBrush"] = new SolidColorBrush(HexToColor(theme.Colors.ZebraRow));
        app.Resources["TableRowHoverBrush"] = new SolidColorBrush(HexToColor(theme.Colors.TableRowHover));
        app.Resources["TableRowSelectedBrush"] = new SolidColorBrush(HexToColor(theme.Colors.TableRowSelected));
        app.Resources["LogBackgroundBrush"] = new SolidColorBrush(HexToColor(theme.Colors.LogBackground));

        // Fonts
        app.Resources["FontFamily"] = BuildFontFamily(theme.Fonts.FontFamily);
        app.Resources["HeadingFontFamily"] = BuildFontFamily(theme.Fonts.HeadingFontFamily);
        app.Resources["FontSize"] = theme.Fonts.GeneralFontSize;
        app.Resources["HeadingFontSize"] = theme.Fonts.HeadingFontSize;
        app.Resources["ButtonFontSize"] = theme.Fonts.ButtonFontSize;
        app.Resources["InputFontSize"] = theme.Fonts.InputFontSize;
        app.Resources["LabelFontSize"] = theme.Fonts.LabelFontSize;
        app.Resources["LogFontSize"] = theme.Fonts.LogFontSize;
        app.Resources["SidebarFontSize"] = theme.Fonts.SidebarFontSize;

        // Dimensions
        app.Resources["ButtonWidth"] = theme.Dimensions.ButtonWidth;
        app.Resources["ButtonHeight"] = theme.Dimensions.ButtonHeight;
        app.Resources["InputHeight"] = theme.Dimensions.InputHeight;
        app.Resources["BorderRadius"] = theme.Dimensions.BorderRadius;
        app.Resources["ControlSpacing"] = theme.Dimensions.ControlSpacing;
        app.Resources["PaddingSize"] = theme.Dimensions.Padding;

        // Button layout - ALWAYS Vertical for sidebar navigation
        var sidebarOrientation = Orientation.Vertical;
        app.Resources["SidebarOrientation"] = sidebarOrientation;

        // Layout direction - فقط Resource ها رو آپدیت کن
        var dir = theme.LayoutDirection;
        app.Resources["MainWindowFlowDirection"] = ToFlowDirection(dir.MainWindowDirection);
        app.Resources["TextFlowDirection"] = ToFlowDirection(dir.TextDirection);
        app.Resources["TableFlowDirection"] = ToFlowDirection(dir.TableDirection);
        app.Resources["MenuFlowDirection"] = ToFlowDirection(dir.MenuDirection);

        // Language برای BiDi (فارسی)
        var xmlLang = System.Windows.Markup.XmlLanguage.GetLanguage("fa-IR");

        // Update sidebar width + Language
        foreach (Window w in app.Windows)
        {
            if (w is MainWindow mw)
            {
                mw.Language = xmlLang;
                try
                {
                    // مهم: در MainWindow.xaml ستون ۰ = سایدبار (سمت راست در RTL) و
                    // ستون ۱ = محتوای اصلی که باید همیشه * بماند. قبلاً کد «آخرین ستون»
                    // را تنظیم می‌کرد و عرض محتوای اصلی را به ۲۶۴ پیکسل قفل می‌کرد
                    // (همان نوار سفید بزرگ سمت چپ).
                    if (mw.Content is System.Windows.Controls.Grid mainGrid &&
                        mainGrid.ColumnDefinitions.Count >= 2)
                    {
                        var sidebarCol = mainGrid.ColumnDefinitions[0];
                        if (!sidebarCol.Width.IsStar)
                        {
                            var width = theme.Dimensions.SidebarWidth;
                            if (width < 180) width = 180;   // جلوگیری از عرض غیرقابل‌استفاده
                            sidebarCol.Width = new System.Windows.GridLength(width);
                        }

                        var contentCol = mainGrid.ColumnDefinitions[1];
                        if (!contentCol.Width.IsStar)
                            contentCol.Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star);
                    }
                }
                catch { }
            }
        }
    }

    private static System.Windows.FlowDirection ToFlowDirection(string value)
    {
        return string.Equals(value, "LeftToRight", StringComparison.OrdinalIgnoreCase)
            ? System.Windows.FlowDirection.LeftToRight
            : System.Windows.FlowDirection.RightToLeft;
    }

    /// <summary>
    /// Builds a FontFamily safely. The bundled Vazir font is referenced through a
    /// pack:// URI so it resolves even on machines where it is not installed.
    /// </summary>
    private static FontFamily BuildFontFamily(string name)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name)) name = "Vazir";
            if (name.StartsWith("pack://", StringComparison.OrdinalIgnoreCase))
                return new FontFamily(name);

            if (string.Equals(name, "Vazir", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri("pack://application:,,,/Fonts/Vazirmatn-Regular.ttf", UriKind.Absolute);
                return new FontFamily(uri, "#Vazir");
            }

            return new FontFamily(name);
        }
        catch
        {
            try { return new FontFamily("Tahoma"); }
            catch { return new FontFamily(); }
        }
    }

    private static Color HexToColor(string hex)
    {
        try
        {
            if (!hex.StartsWith("#")) hex = "#" + hex;
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch { return Colors.Gray; }
    }

    private static string SerializeDict(Dictionary<string, string> dict)
    {
        return string.Join("|", dict.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
    }

    private static Dictionary<string, string> DeserializeDict(string str)
    {
        var dict = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(str)) return dict;
        foreach (var part in str.Split('|'))
        {
            var eq = part.IndexOf('=');
            if (eq > 0)
            {
                var key = Uri.UnescapeDataString(part[..eq]);
                var val = Uri.UnescapeDataString(part[(eq + 1)..]);
                dict[key] = val;
            }
        }
        return dict;
    }
}
