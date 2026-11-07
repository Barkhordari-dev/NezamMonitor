namespace NezamMonitorPro.Core.Theme;

/// <summary>
/// A complete theme definition (colors + fonts + dimensions + layout).
/// </summary>
public sealed class AppTheme
{
    public string Name { get; set; } = "";
    public string PersianName { get; set; } = "";
    public ThemeColors Colors { get; set; } = new();
    public ThemeFonts Fonts { get; set; } = new();
    public ThemeDimensions Dimensions { get; set; } = new();
    public ButtonLayout ButtonLayout { get; set; } = new();
    public LayoutDirection LayoutDirection { get; set; } = new();

    public AppTheme Clone()
    {
        return new AppTheme
        {
            Name = Name,
            PersianName = PersianName,
            Colors = Colors.Clone(),
            Fonts = Fonts.Clone(),
            Dimensions = Dimensions.Clone(),
            ButtonLayout = ButtonLayout.Clone(),
            LayoutDirection = LayoutDirection.Clone(),
        };
    }
}

/// <summary>
/// Built-in themes with pre-configured palettes.
/// All colors are hex strings to avoid WPF dependency.
/// </summary>
public static class BuiltInThemes
{
    /// <summary>نسخه پرو — پالت مدرن آبی/بنفش با سایدبار تیره</summary>
    /// <summary>نسخه پرو — تیره (Dark Pro)</summary>
    /// <summary>دخترانه — پالت صورتی روشن اصلی (همان نسخه‌ی قبلی که کاربر
    /// ترجیح می‌دهد)، با رنگ‌های ناقص آن کامل شده تا در تم‌های تیره/روشن
    /// به مقادیر پیش‌فرض روشن برنگردد. SidebarActive صورتی روشن
    /// (#FFB6C1) همان مقدار اصلی است؛ در رابط فعلی مصرف‌کننده‌ای ندارد
    /// (آیتم فعال سایدبار از SidebarActiveSoft استفاده می‌کند).</summary>
    public static AppTheme Girly => new()
    {
        Name = "Girly",
        PersianName = "دخترانه",
        Colors = new ThemeColors
        {
            Primary = "#FFE91E63",
            Secondary = "#FF9C27B0",
            Accent = "#FFFFC107",
            Background = "#FFFFF0F5",
            Surface = "#FFFFE4E1",
            Panel = "#FFFFD1DC",
            Card = "#FFFFFFFF",
            ButtonPrimary = "#FFE91E63",
            ButtonPrimaryHover = "#FFC2185B",
            ButtonPrimaryPressed = "#FFAD1457",
            ButtonDisabled = "#FFF3B9CE",
            InputBackground = "#FFFFFFFF",
            InputBorder = "#FFF48FB1",
            Text = "#FF4A2E4D",
            TextSecondary = "#FF7D5A65",
            Border = "#FFF8BBD0",
            Success = "#FF009688",
            Warning = "#FFFF9800",
            Error = "#FFE53935",
            Info = "#FFAB47BC",
            Sidebar = "#FFC71585",
            SidebarText = "#FFFFFFFF",
            SidebarActive = "#FFFFB6C1",
            SidebarActiveSoft = "#33FFB6C1",
            LogBackground = "#FFC71585",
            SidebarHover = "#14FFFFFF",
            HoverOverlay = "#14FFFFFF",
            ShadowColor = "#40E91E63",
        },
        Dimensions = new ThemeDimensions
        {
            BorderRadius = 12,
            ControlSpacing = 10,
            ButtonHeight = 40,
            InputHeight = 34,
            SidebarWidth = 264,
        },
    };

    /// <summary>مردانه — سفید/مشکی/طوسی قدرتی با اکسنت قرمز قدرت و گوشه‌های تیز</summary>
    public static AppTheme Masculine => new()
    {
        Name = "Masculine",
        PersianName = "مردانه",
        Colors = new ThemeColors
        {
            Primary = "#FF18181B",
            Secondary = "#FF71717A",
            Accent = "#FFDC2626",
            Background = "#FFF4F4F5",
            Surface = "#FFFFFFFF",
            Panel = "#FFE4E4E7",
            Card = "#FFFFFFFF",
            ButtonPrimary = "#FF18181B",
            ButtonPrimaryHover = "#FF000000",
            ButtonPrimaryPressed = "#FF000000",
            ButtonDisabled = "#FFA1A1AA",
            InputBackground = "#FFFFFFFF",
            InputBorder = "#FFA1A1AA",
            Text = "#FF09090B",
            TextSecondary = "#FF52525B",
            Border = "#FFD4D4D8",
            Success = "#FF16A34A",
            Warning = "#FFEA580C",
            Error = "#FFDC2626",
            Info = "#FF3F3F46",
            Sidebar = "#FF09090B",
            SidebarText = "#FFF4F4F5",
            SidebarActive = "#FFDC2626",
            SidebarActiveSoft = "#33DC2626",
            LogBackground = "#FF09090B",
            SidebarHover = "#14FFFFFF",
            HoverOverlay = "#14FFFFFF",
            ShadowColor = "#40000000",
        },
        Dimensions = new ThemeDimensions
        {
            BorderRadius = 6,
            ControlSpacing = 10,
            ButtonHeight = 42,
            InputHeight = 34,
            SidebarWidth = 264,
        },
    };

    /// <summary>زمردی — سبز عمیق لوکس با اکسنت طلایی و سایدبار سبز تیره</summary>
    public static AppTheme Emerald => new()
    {
        Name = "Emerald",
        PersianName = "زمردی",
        Colors = new ThemeColors
        {
            Primary = "#FF047857",
            Secondary = "#FF10B981",
            Accent = "#FFD97706",
            Background = "#FFECFDF5",
            Surface = "#FFFFFFFF",
            Panel = "#FFD1FAE5",
            Card = "#FFFFFFFF",
            ButtonPrimary = "#FF047857",
            ButtonPrimaryHover = "#FF065F46",
            ButtonPrimaryPressed = "#FF064E3B",
            ButtonDisabled = "#FFA7D9C5",
            InputBackground = "#FFFFFFFF",
            InputBorder = "#FF6EE7B7",
            Text = "#FF022C22",
            TextSecondary = "#FF065F46",
            Border = "#FFA7F3D0",
            Success = "#FF059669",
            Warning = "#FFD97706",
            Error = "#FFDC2626",
            Info = "#FF047857",
            Sidebar = "#FF022C22",
            SidebarText = "#FFD1FAE5",
            SidebarActive = "#FF047857",
            SidebarActiveSoft = "#33047857",
            LogBackground = "#FF022C22",
            SidebarHover = "#14FFFFFF",
            HoverOverlay = "#14FFFFFF",
            ShadowColor = "#40047857",
        },
        Dimensions = new ThemeDimensions
        {
            BorderRadius = 10,
            ControlSpacing = 10,
            ButtonHeight = 40,
            InputHeight = 34,
            SidebarWidth = 264,
        },
    };

    public static AppTheme ProDark => new()
    {
        Name = "ProDark",
        PersianName = "پرو تیره",
        Colors = new ThemeColors
        {
            Primary = "#FF60A5FA",
            Secondary = "#FF60A5FA",
            Accent = "#FFA78BFA",
            Background = "#FF0B1120",
            Surface = "#FF0F172A",
            Panel = "#FF1E293B",
            Card = "#FF16213A",
            ButtonPrimary = "#FF2563EB",
            ButtonPrimaryHover = "#FF1D4ED8",
            ButtonPrimaryPressed = "#FF1E40AF",
            ButtonDisabled = "#FF475569",
            InputBackground = "#FF0F172A",
            InputBorder = "#FF334155",
            Text = "#FFF8FAFC",
            TextSecondary = "#FFB8C7DA",
            Border = "#FF263449",
            Success = "#FF10B981",
            Warning = "#FFF59E0B",
            Error = "#FFEF4444",
            Info = "#FF3B82F6",
            Sidebar = "#FF0F172A",
            SidebarText = "#FFCBD5E1",
            SidebarActive = "#FF60A5FA",
            SidebarActiveSoft = "#203B82F6",
            LogBackground = "#FF020617",
            SidebarHover = "#10FFFFFF",
            HoverOverlay = "#10FFFFFF",
            ShadowColor = "#40000000",
        },
        Dimensions = new ThemeDimensions
        {
            BorderRadius = 10,
            ControlSpacing = 10,
            ButtonHeight = 40,
            InputHeight = 34,
            SidebarWidth = 264,
        },
    };

    public static AppTheme Light => new()
    {
        Name = "Light",
        PersianName = "روشن",
        Colors = new ThemeColors
        {
            Primary = "#FF3498DB",
            Secondary = "#FF2ECC71",
            Accent = "#FF9B59B6",
            Background = "#FFF5F5F5",
            Surface = "#FFFFFFFF",
            Panel = "#FFECEFF1",
            Card = "#FFFFFFFF",
            ButtonPrimary = "#FF3498DB",
            ButtonPrimaryHover = "#FF2980B9",
            ButtonPrimaryPressed = "#FF246EA0",
            ButtonDisabled = "#FFBDC3C7",
            InputBackground = "#FFFFFFFF",
            InputBorder = "#FF94A3B8",
            Text = "#FF111827",
            TextSecondary = "#FF4B5563",
            Border = "#FFD1D5DB",
            Success = "#FF2ECC71",
            Warning = "#FFF1C40F",
            Error = "#FFE74C3C",
            Info = "#FF3498DB",
            Sidebar = "#FF2C3E50",
            SidebarText = "#FFF1F5F9",
            SidebarActive = "#FF1D4ED8",
            SidebarActiveSoft = "#1A2A405E",
            LogBackground = "#FF2C3E50",
        },
    };

    public static AppTheme Professional => new()
    {
        Name = "Professional",
        PersianName = "حرفه‌ای",
        Colors = new ThemeColors
        {
            Primary = "#FF004785",
            Secondary = "#FF009688",
            Accent = "#FF795548",
            Background = "#FFF8F9FA",
            Surface = "#FFFFFFFF",
            Panel = "#FFF1F3F5",
            Card = "#FFFFFFFF",
            ButtonPrimary = "#FF004785",
            ButtonPrimaryHover = "#FF00386A",
            ButtonPrimaryPressed = "#FF002850",
            ButtonDisabled = "#FFC8C8C8",
            InputBackground = "#FFFFFFFF",
            InputBorder = "#FF8A9BAE",
            Text = "#FF14202E",
            TextSecondary = "#FF45566B",
            Border = "#FFCBD5E1",
            Success = "#FF009688",
            Warning = "#FFFF9800",
            Error = "#FFC62828",
            Info = "#FF004785",
            Sidebar = "#FF003366",
            SidebarText = "#FFDCE7F2",
            SidebarActive = "#FF00695C",
            SidebarActiveSoft = "#1A003865",
            LogBackground = "#FF002850",
        },
    };

    public static List<AppTheme> GetAllThemes() => new()
    {
        Light, Professional, ProDark, Girly, Masculine, Emerald,
    };

    public static List<AppTheme> GetAllLayoutPresets() => new()
    {
        Light, Professional, ProDark, Girly, Masculine, Emerald,
    };

    public static Dictionary<string, string> ThemeNameMap => new()
    {
        ["Light"] = "روشن",
        ["Professional"] = "حرفه‌ای",
        ["ProDark"] = "پرو تیره",
        ["Girly"] = "دخترانه",
        ["Masculine"] = "مردانه",
        ["Emerald"] = "زمردی",
    };
}
