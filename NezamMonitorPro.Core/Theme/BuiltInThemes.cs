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
    public static AppTheme Pro => new()
    {
        Name = "Pro",
        PersianName = "پرو",
        Colors = new ThemeColors
        {
            Primary = "#FF2563EB",
            Secondary = "#FF0EA5E9",
            Accent = "#FF7C3AED",
            Background = "#FFF1F5F9",
            Surface = "#FFFFFFFF",
            Panel = "#FFF8FAFC",
            Card = "#FFFFFFFF",
            ButtonPrimary = "#FF2563EB",
            ButtonPrimaryHover = "#FF1D4ED8",
            ButtonPrimaryPressed = "#FF1E40AF",
            ButtonDisabled = "#FFCBD5E1",
            InputBackground = "#FFFFFFFF",
            InputBorder = "#FF94A3B8",
            Text = "#FF0F172A",
            TextSecondary = "#FF44546A",
            Border = "#FFCBD5E1",
            Success = "#FF10B981",
            Warning = "#FFF59E0B",
            Error = "#FFEF4444",
            Info = "#FF0EA5E9",
            Sidebar = "#FF0B1220",
            SidebarText = "#FFE2E8F0",
            SidebarActive = "#FF1D4ED8",
            LogBackground = "#FF0B1220",
            SidebarHover = "#1AFFFFFF",
            HoverOverlay = "#14000000",
        },
        Dimensions = new ThemeDimensions
        {
            BorderRadius = 8,
            ControlSpacing = 10,
            ButtonHeight = 40,
            InputHeight = 34,
            SidebarWidth = 264,
        },
        Fonts = new ThemeFonts
        {
            GeneralFontSize = 13.5,
            ButtonFontSize = 13,
            InputFontSize = 13,
            LabelFontSize = 12,
            LogFontSize = 11,
            SidebarFontSize = 15,
            HeadingFontSize = 22,
        },
    };

    /// <summary>نسخه پرو — تیره (Dark Pro)</summary>
    public static AppTheme ProDark => new()
    {
        Name = "ProDark",
        PersianName = "پرو تیره",
        Colors = new ThemeColors
        {
            Primary = "#FF3B82F6",
            Secondary = "#FF22D3EE",
            Accent = "#FFA855F7",
            Background = "#FF0B1220",
            Surface = "#FF111827",
            Panel = "#FF1A2233",
            Card = "#FF151D2B",
            ButtonPrimary = "#FF3B82F6",
            ButtonPrimaryHover = "#FF2563EB",
            ButtonPrimaryPressed = "#FF1D4ED8",
            ButtonDisabled = "#FF334155",
            InputBackground = "#FF0F172A",
            InputBorder = "#FF64748B",
            Text = "#FFF1F5F9",
            TextSecondary = "#FFA8B8CC",
            Border = "#FF334155",
            Success = "#FF34D399",
            Warning = "#FFFBBF24",
            Error = "#FFF87171",
            Info = "#FF38BDF8",
            Sidebar = "#FF020617",
            SidebarText = "#FFD7E0EE",
            SidebarActive = "#FF2563EB",
            LogBackground = "#FF020617",
            SidebarHover = "#1AFFFFFF",
            HoverOverlay = "#1A94A3B8",
        },
        Dimensions = new ThemeDimensions
        {
            BorderRadius = 8,
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
            LogBackground = "#FF2C3E50",
        },
    };

    public static AppTheme Dark => new()
    {
        Name = "Dark",
        PersianName = "تاریک",
        Colors = new ThemeColors
        {
            Primary = "#FF5299DB",
            Secondary = "#FF2ECC71",
            Accent = "#FF9B59B6",
            Background = "#FF1E1E1E",
            Surface = "#FF2D2D2D",
            Panel = "#FF333333",
            Card = "#FF373737",
            ButtonPrimary = "#FF5299DB",
            ButtonPrimaryHover = "#FF4180B9",
            ButtonPrimaryPressed = "#FF246EA0",
            ButtonDisabled = "#FF505050",
            InputBackground = "#FF3C3C3C",
            InputBorder = "#FF707070",
            Text = "#FFF1F5F9",
            TextSecondary = "#FFB8C4D4",
            Border = "#FF4A4A4A",
            Success = "#FF2ECC71",
            Warning = "#FFF1C40F",
            Error = "#FFE74C3C",
            Info = "#FF5299DB",
            Sidebar = "#FF191919",
            SidebarText = "#FFD7E0EE",
            SidebarActive = "#FF2563EB",
            LogBackground = "#FF141414",
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
            LogBackground = "#FF002850",
        },
    };

    public static AppTheme Modern => new()
    {
        Name = "Modern",
        PersianName = "مدرن",
        Colors = new ThemeColors
        {
            Primary = "#FF6366F1",
            Secondary = "#FF10B981",
            Accent = "#FFF43B5E",
            Background = "#FFF9FAFB",
            Surface = "#FFFFFFFF",
            Panel = "#FFF3F4F6",
            Card = "#FFFFFFFF",
            ButtonPrimary = "#FF6366F1",
            ButtonPrimaryHover = "#FF4F52E1",
            ButtonPrimaryPressed = "#FF3B3EC9",
            ButtonDisabled = "#FFD1D5DB",
            InputBackground = "#FFFFFFFF",
            InputBorder = "#FF9CA3AF",
            Text = "#FF111827",
            TextSecondary = "#FF4B5563",
            Border = "#FFD1D5DB",
            Success = "#FF10B981",
            Warning = "#FFF59E0B",
            Error = "#FFEF4444",
            Info = "#FF6366F1",
            Sidebar = "#FF111827",
            SidebarText = "#FFE5E7EB",
            SidebarActive = "#FF4F46E5",
            LogBackground = "#FF1F2937",
        },
        Dimensions = new ThemeDimensions
        {
            BorderRadius = 10,
            ControlSpacing = 10,
            ButtonHeight = 44,
        },
    };

    public static AppTheme HighContrast => new()
    {
        Name = "HighContrast",
        PersianName = "کنتراست بالا",
        Colors = new ThemeColors
        {
            Primary = "#FF0000FF",
            Secondary = "#FF008000",
            Accent = "#FF800080",
            Background = "#FF000000",
            Surface = "#FF000000",
            Panel = "#FF141414",
            Card = "#FF1E1E1E",
            ButtonPrimary = "#FF0000FF",
            ButtonPrimaryHover = "#FF3232FF",
            ButtonPrimaryPressed = "#FF6464FF",
            ButtonDisabled = "#FF505050",
            InputBackground = "#FF141414",
            InputBorder = "#FFC0C0C0",
            Text = "#FFFFFFFF",
            TextSecondary = "#FFE0E0E0",
            Border = "#FFA0A0A0",
            Success = "#FF00FF00",
            Warning = "#FFFFFF00",
            Error = "#FFFF0000",
            Info = "#FF0096FF",
            Sidebar = "#FF0A0A0A",
            SidebarText = "#FFFFFFFF",
            SidebarActive = "#FF0000FF",
            LogBackground = "#FF000000",
        },
    };

    public static AppTheme Spacious => new()
    {
        Name = "Spacious",
        PersianName = "جادار",
        Colors = new ThemeColors(),
        Dimensions = new ThemeDimensions
        {
            WindowWidth = 1400,
            WindowHeight = 900,
            ButtonWidth = 220,
            ButtonHeight = 48,
            InputHeight = 38,
            BorderRadius = 8,
            Padding = 24,
            Margin = 12,
            ControlSpacing = 12,
            SidebarWidth = 240,
        },
        Fonts = new ThemeFonts
        {
            GeneralFontSize = 16,
            ButtonFontSize = 15,
            InputFontSize = 15,
            LabelFontSize = 15,
            LogFontSize = 13,
            SidebarFontSize = 16,
            HeadingFontSize = 24,
        },
    };

    public static List<AppTheme> GetAllThemes() => new()
    {
        Pro, ProDark, Light, Dark, Professional, Modern, HighContrast,
    };

    public static List<AppTheme> GetAllLayoutPresets() => new()
    {
        Pro, Light, Spacious, Professional, Modern,
    };

    public static Dictionary<string, string> ThemeNameMap => new()
    {
        ["Pro"] = "پرو",
        ["ProDark"] = "پرو تیره",
        ["Light"] = "روشن",
        ["Dark"] = "تاریک",
        ["Professional"] = "حرفهای",
        ["Modern"] = "مدرن",
        ["HighContrast"] = "کنتراست بالا",
        ["Spacious"] = "جادار",
    };
}
