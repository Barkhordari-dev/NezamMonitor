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
            Background = "#FFF8FAFC",
            Surface = "#FFF5F7FB",
            Panel = "#FFF1F5F9",
            Card = "#FFFCFDFE",
            ButtonPrimary = "#FF2563EB",
            ButtonPrimaryHover = "#FF1D4ED8",
            ButtonPrimaryPressed = "#FF1E40AF",
            ButtonDisabled = "#FFCBD5E1",
            InputBackground = "#FFF9FAFB",
            InputBorder = "#FFCBD5E1",
            Text = "#FF0F172A",
            TextSecondary = "#FF475569",
            Border = "#FFE2E8F0",
            Success = "#FF10B981",
            Warning = "#FFF59E0B",
            Error = "#FFEF4444",
            Info = "#FF0EA5E9",
            Sidebar = "#FF1E293B",
            SidebarText = "#FFE2E8F0",
            SidebarActive = "#FF2563EB",
            SidebarActiveSoft = "#26FFFFFF",
            LogBackground = "#FF0B1220",
            SidebarHover = "#14FFFFFF",
            HoverOverlay = "#14000000",
            ShadowColor = "#1A0B1B33",
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
            SidebarFontSize = 17,
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
            Secondary = "#FF60A5FA",
            Accent = "#FF8B5CF6",
            Background = "#FF0F172A",
            Surface = "#FF1E293B",
            Panel = "#FF334155",
            Card = "#FF1E293B",
            ButtonPrimary = "#FF2563EB",
            ButtonPrimaryHover = "#FF1D4ED8",
            ButtonPrimaryPressed = "#FF1E40AF",
            ButtonDisabled = "#FF475569",
            InputBackground = "#FF0F172A",
            InputBorder = "#FF334155",
            Text = "#FFF8FAFC",
            TextSecondary = "#FFB8C7DA",
            Border = "#FF334155",
            Success = "#FF10B981",
            Warning = "#FFF59E0B",
            Error = "#FFEF4444",
            Info = "#FF3B82F6",
            Sidebar = "#FF020617",
            SidebarText = "#FFCBD5E1",
            SidebarActive = "#FF3B82F6",
            SidebarActiveSoft = "#203B82F6",
            LogBackground = "#FF020617",
            SidebarHover = "#10FFFFFF",
            HoverOverlay = "#10FFFFFF",
            ShadowColor = "#40000000",
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
            SidebarActiveSoft = "#1A2A405E",
            LogBackground = "#FF2C3E50",
        },
    };

    public static AppTheme Dark => new()
    {
        Name = "Dark",
        PersianName = "تاریک",
        Colors = new ThemeColors
        {
            Primary = "#FF60A5FA",
            Secondary = "#FF34D399",
            Accent = "#FFA5B4FC",
            Background = "#FF121212",
            Surface = "#FF1E1E1E",
            Panel = "#FF2D2D2D",
            Card = "#FF1E1E1E",
            ButtonPrimary = "#FF2563EB",
            ButtonPrimaryHover = "#FF1D4ED8",
            ButtonPrimaryPressed = "#FF1E40AF",
            ButtonDisabled = "#FF333333",
            InputBackground = "#FF121212",
            InputBorder = "#FF404040",
            Text = "#FFE2E8F0",
            TextSecondary = "#FF94A3B8",
            Border = "#FF333333",
            Success = "#FF34D399",
            Warning = "#FFFBBF24",
            Error = "#FFF87171",
            Info = "#FF60A5FA",
            Sidebar = "#FF181818",
            SidebarText = "#FFCBD5E1",
            SidebarActive = "#FF3B82F6",
            SidebarActiveSoft = "#1A3B82F6",
            LogBackground = "#FF0A0A0A",
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
            SidebarActiveSoft = "#1A171D3A",
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
            Primary = "#FF2563EB",
            Secondary = "#FF00FF00",
            Accent = "#FFFFFF00",
            Background = "#FF000000",
            Surface = "#FF000000",
            Panel = "#FF000000",
            Card = "#FF000000",
            ButtonPrimary = "#FF2563EB",
            ButtonPrimaryHover = "#FF1D4ED8",
            ButtonPrimaryPressed = "#FF1E40AF",
            ButtonDisabled = "#FF808080",
            InputBackground = "#FF000000",
            InputBorder = "#FFFFFFFF",
            Text = "#FFFFFFFF",
            TextSecondary = "#FFFFFFFF",
            Border = "#FFFFFFFF",
            Success = "#FF00FF00",
            Warning = "#FFFFFF00",
            Error = "#FFFF0000",
            Info = "#FF00FFFF",
            Sidebar = "#FF000000",
            SidebarText = "#FFFFFFFF",
            SidebarActive = "#FF2563EB",
            SidebarActiveSoft = "#40FFFFFF",
            LogBackground = "#FF000000",
        },
    };

    public static AppTheme Fantasy => new()
    {
        Name = "Fantasy",
        PersianName = "فانتزی",
        Colors = new ThemeColors
        {
            Primary = "#FF00F0FF",
            Secondary = "#FFD946EF",
            Accent = "#FFFACC15",
            Background = "#FF0F051D",
            Surface = "#FF1E0B36",
            Panel = "#FF2D1252",
            Card = "#FF1E0B36",
            ButtonPrimary = "#FF4B0082",
            ButtonPrimaryHover = "#FF2A0845",
            ButtonPrimaryPressed = "#FF5D2886",
            InputBackground = "#FF0F051D",
            InputBorder = "#FFD946EF",
            Text = "#FFECFEFF",
            TextSecondary = "#FFC084FC",
            Border = "#FF7C3AED",
            Success = "#FF10B981",
            Warning = "#FFF59E0B",
            Error = "#FFEF4444",
            Info = "#FF00F0FF",
            Sidebar = "#FF0B0214",
            SidebarText = "#FFECFEFF",
            SidebarActive = "#FF00F0FF",
            SidebarActiveSoft = "#3000F0FF",
            LogBackground = "#FF05000A",
            ShadowColor = "#80D946EF",
        },
    };

    public static AppTheme Spacious => new()
    {
        Name = "Spacious",
        PersianName = "جادار",
        Colors = new ThemeColors
        {
            Background = "#FF121212",
            Surface = "#FF1E1E1E",
            Panel = "#FF252525",
            Card = "#FF2D2D2D",
            InputBackground = "#FF1E1E1E",
            InputBorder = "#FF505050",
            Border = "#FF4A4A4A",
            Sidebar = "#FF0F0F0F",
        },
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
            SidebarFontSize = 18,
            HeadingFontSize = 24,
        },
    };

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
            Text = "#FF4A2E4D",
            TextSecondary = "#FF7D5A65",
            Sidebar = "#FFC71585",
            SidebarText = "#FFFFFFFF",
            SidebarActive = "#FFFFB6C1",
        },
    };

    public static List<AppTheme> GetAllThemes() => new()
    {
        Pro, ProDark, Light, Dark, Professional, Modern, HighContrast, Spacious, Fantasy, Girly,
    };

    public static List<AppTheme> GetAllLayoutPresets() => new()
    {
        Pro, Light, Spacious, Professional, Modern, Fantasy, Girly,
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
        ["Fantasy"] = "فانتزی",
        ["Girly"] = "دخترانه",
    };
}
