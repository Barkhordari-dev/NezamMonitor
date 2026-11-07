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
        _currentTheme = BuiltInThemes.Light;
    }

    public AppTheme LoadTheme()
    {
        var settings = _db.LoadSettings();
        var theme = BuiltInThemes.Light;
        var foundSavedTheme = false;

        if (settings.TryGetValue($"{DbPrefix}Name", out var name))
        {
            var allThemes = BuiltInThemes.GetAllThemes();
            var match = allThemes.FirstOrDefault(t => t.Name == name);
            if (match != null) { theme = match.Clone(); foundSavedTheme = true; }
            // A user-customised theme is stored under the name "Custom" with
            // its colours/fonts/dimensions in the Settings table — it is not
            // a deleted built-in theme, so keep it and apply its saved dicts.
            else if (string.Equals(name, "Custom", StringComparison.OrdinalIgnoreCase))
                foundSavedTheme = true;
            // Any other unknown name is a deleted built-in theme: fall through
            // to the migration below (foundSavedTheme stays false).
        }
        else
        {
            // Nothing saved yet (fresh install) — Light is the default, and
            // there is nothing to migrate.
            foundSavedTheme = true;
        }

        // If the saved theme no longer exists (deleted theme, or a custom
        // "Custom" name from the old theme list), do NOT resurrect it by
        // applying the saved colour/font/dimension dictionaries on top of
        // the fallback — migrate the user to Light instead.
        if (!foundSavedTheme)
        {
            _currentTheme = theme;
            SaveTheme(theme);
            return theme;
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
        SaveTableAlignment("Center");
        return defaultTheme;
    }

    /// <summary>Table text alignment setting (theme_TableAlignment):
    /// "Center" (default), "Right" or "Left". Kept outside the theme so a
    /// theme change does not wipe it.</summary>
    public string GetTableAlignment()
    {
        var value = _db.GetSetting("theme_TableAlignment", "Center");
        return value is "Right" or "Left" or "Center" ? value : "Center";
    }

    public void SaveTableAlignment(string value)
    {
        if (value is not ("Right" or "Left" or "Center")) value = "Center";
        _db.SaveSetting("theme_TableAlignment", value);
        ApplyTableAlignment(value);
    }

    private static void ApplyTableAlignment(string value)
    {
        var app = Application.Current;
        if (app == null) return;
        // The setting means a PHYSICAL direction. HorizontalContentAlignment
        // is physical, but TextAlignment is flow-direction-relative in WPF:
        // in an RTL paragraph Left lands on the physical right and Right on
        // the physical left. Setting both resources to the same value
        // therefore put the cell text and the header on OPPOSITE sides
        // (invisible while the default was Center, which is symmetric).
        // So the TextAlignment value is inverted in RTL.
        var rtl = app.Resources["TableFlowDirection"] is not FlowDirection fd
                  || fd == FlowDirection.RightToLeft;
        app.Resources["TableTextAlignment"] = value switch
        {
            "Right" => rtl ? TextAlignment.Left : TextAlignment.Right,
            "Left" => rtl ? TextAlignment.Right : TextAlignment.Left,
            _ => TextAlignment.Center,
        };
        app.Resources["TableContentAlignment"] = value switch
        {
            "Right" => HorizontalAlignment.Right,
            "Left" => HorizontalAlignment.Left,
            _ => HorizontalAlignment.Center,
        };
    }

    public void ApplyTheme(AppTheme? theme = null)
    {
        theme ??= _currentTheme;

        // Derive the soft active-sidebar tint from the current colours so it
        // always matches the theme, including themes restored from the
        // Settings table that were saved before this colour existed.
        try { theme.Colors.DeriveSidebarActiveSoft(); } catch { }
        try { theme.Colors.DeriveTableTints(); } catch { }
        try { theme.Colors.DeriveOnColors(); } catch { }
        try { theme.Colors.DeriveTextColors(); } catch { }

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
        // Text that goes ON a filled colour (buttons, badges) — white or
        // near-black, whichever reads better on that fill in this theme.
        app.Resources["OnPrimaryBrush"] = new SolidColorBrush(HexToColor(theme.Colors.OnPrimary));
        app.Resources["OnAccentBrush"] = new SolidColorBrush(HexToColor(theme.Colors.OnAccent));
        app.Resources["OnSuccessBrush"] = new SolidColorBrush(HexToColor(theme.Colors.OnSuccess));
        app.Resources["OnWarningBrush"] = new SolidColorBrush(HexToColor(theme.Colors.OnWarning));
        app.Resources["OnErrorBrush"] = new SolidColorBrush(HexToColor(theme.Colors.OnError));
        app.Resources["OnInfoBrush"] = new SolidColorBrush(HexToColor(theme.Colors.OnInfo));
        app.Resources["OnSecondaryBrush"] = new SolidColorBrush(HexToColor(theme.Colors.OnSecondary));
        // Coloured text that goes ON a card/surface (dashboard numbers, ...).
        app.Resources["PrimaryTextBrush"] = new SolidColorBrush(HexToColor(theme.Colors.PrimaryText));
        app.Resources["AccentTextBrush"] = new SolidColorBrush(HexToColor(theme.Colors.AccentText));
        app.Resources["SuccessTextBrush"] = new SolidColorBrush(HexToColor(theme.Colors.SuccessText));
        app.Resources["WarningTextBrush"] = new SolidColorBrush(HexToColor(theme.Colors.WarningText));
        app.Resources["ErrorTextBrush"] = new SolidColorBrush(HexToColor(theme.Colors.ErrorText));
        app.Resources["SidebarBrush"] = new SolidColorBrush(HexToColor(theme.Colors.Sidebar));
        app.Resources["SidebarTextBrush"] = new SolidColorBrush(HexToColor(theme.Colors.SidebarText));
        app.Resources["SidebarActiveBrush"] = new SolidColorBrush(HexToColor(theme.Colors.SidebarActive));
        app.Resources["SidebarActiveSoftBrush"] = new SolidColorBrush(HexToColor(theme.Colors.SidebarActiveSoft));
        app.Resources["SidebarHoverBrush"] = new SolidColorBrush(HexToColor(theme.Colors.SidebarHover));
        app.Resources["HoverOverlayBrush"] = new SolidColorBrush(HexToColor(theme.Colors.HoverOverlay));
        app.Resources["ZebraRowBrush"] = new SolidColorBrush(HexToColor(theme.Colors.ZebraRow));
        app.Resources["TableRowHoverBrush"] = new SolidColorBrush(HexToColor(theme.Colors.TableRowHover));
        app.Resources["TableRowSelectedBrush"] = new SolidColorBrush(HexToColor(theme.Colors.TableRowSelected));
        app.Resources["ShadowColorBrush"] = new SolidColorBrush(HexToColor(theme.Colors.ShadowColor));
        app.Resources["ShadowColor"] = HexToColor(theme.Colors.ShadowColor);
        app.Resources["LogBackgroundBrush"] = new SolidColorBrush(HexToColor(theme.Colors.LogBackground));

        // Fonts
        app.Resources["FontFamily"] = BuildFontFamily(theme.Fonts.FontFamily);
        app.Resources["HeadingFontFamily"] = BuildFontFamily(theme.Fonts.HeadingFontFamily);
        app.Resources["FontSize"] = theme.Fonts.GeneralFontSize;
        app.Resources["HeadingFontSize"] = theme.Fonts.HeadingFontSize;
        app.Resources["ButtonFontSize"] = theme.Fonts.ButtonFontSize;
        app.Resources["InputFontSize"] = theme.Fonts.InputFontSize;
        app.Resources["LabelFontSize"] = theme.Fonts.LabelFontSize;
        app.Resources["CaptionFontSize"] = theme.Fonts.LabelFontSize - 1.5;
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

        // Table text alignment — read from its own setting (not from the
        // theme), so changing the theme never resets it. Default: Center.
        ApplyTableAlignment(GetTableAlignment());

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
                        // حذف تغییر عرض دستی برای جلوگیری از شکنندگی
                        // sidebarCol.Width = new System.Windows.GridLength(width);


                        var contentCol = mainGrid.ColumnDefinitions[1];
                        // contentCol.Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star);
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
    /// Builds a FontFamily safely. The bundled B Nazanin and Vazir fonts are
    /// referenced through pack:// URIs so they resolve even on machines where
    /// they are not installed; other names fall back to Tahoma, then Arial.
    ///
    /// NOTE — why the two-argument constructor: the previous code built a
    /// single string "pack://application:,,,/Fonts/BNazanin.ttf#B Nazanin,
    /// Tahoma, Arial". WPF splits a FontFamily string on commas, and the
    /// pack:// URI itself contains commas ("application:,,,"), so the URI
    /// was torn apart and the font never resolved — the app silently fell
    /// back to Tahoma. That is why picking "B Nazanin" in Settings changed
    /// nothing (the app was already rendering Tahoma) while picking Arial
    /// (a plain system-font name) worked. The documented
    /// FontFamily(baseUri, "./Fonts/File.ttf#Family") form has no such
    /// parsing problem. The GlyphTypeface probe keeps the behaviour safe:
    /// if the bundled file is missing from the build, the name falls back
    /// to an installed copy of the font, then Tahoma, then Arial.
    /// </summary>
    private static FontFamily BuildFontFamily(string name)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name)) name = "B Nazanin";

            if (name.StartsWith("pack://", StringComparison.OrdinalIgnoreCase))
                return new FontFamily(name);

            if (string.Equals(name, "B Nazanin", StringComparison.OrdinalIgnoreCase)
                && EmbeddedFontExists("Fonts/BNazanin.ttf"))
            {
                return new FontFamily(new Uri("pack://application:,,,/"),
                    "./Fonts/BNazanin.ttf#B Nazanin");
            }

            if (string.Equals(name, "Vazir", StringComparison.OrdinalIgnoreCase)
                && EmbeddedFontExists("Fonts/Vazirmatn-Regular.ttf"))
            {
                return new FontFamily(new Uri("pack://application:,,,/"),
                    "./Fonts/Vazirmatn-Regular.ttf#Vazir");
            }

            // System font (or an installed copy of a bundled name): a plain
            // comma list is parsed correctly because it contains no URI.
            return new FontFamily($"{name}, Tahoma, Arial");
        }
        catch
        {
            try { return new FontFamily("Tahoma, Arial"); }
            catch { return new FontFamily(); }
        }
    }

    /// <summary>True when the bundled font file exists as an application
    /// resource (i.e. it was included in the build as a Resource).</summary>
    private static bool EmbeddedFontExists(string relativePath)
    {
        try
        {
            var uri = new Uri($"pack://application:,,,/{relativePath}", UriKind.Absolute);
            _ = new System.Windows.Media.GlyphTypeface(uri);
            return true;
        }
        catch
        {
            return false;
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
