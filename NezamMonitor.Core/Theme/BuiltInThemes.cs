namespace NezamMonitor.Core.Theme;

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
            InputBorder = "#FFBDC3C7",
            Text = "#FF2C3E50",
            TextSecondary = "#FF7F8C8D",
            Border = "#FFBDC3C7",
            Success = "#FF2ECC71",
            Warning = "#FFF1C40F",
            Error = "#FFE74C3C",
            Info = "#FF3498DB",
            Sidebar = "#FF2C3E50",
            SidebarText = "#FFFFFFFF",
            SidebarActive = "#FF3498DB",
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
            InputBorder = "#FF505050",
            Text = "#FFECF0F1",
            TextSecondary = "#FFAAAAAA",
            Border = "#FF464646",
            Success = "#FF2ECC71",
            Warning = "#FFF1C40F",
            Error = "#FFE74C3C",
            Info = "#FF5299DB",
            Sidebar = "#FF191919",
            SidebarText = "#FFDCDCDC",
            SidebarActive = "#FF5299DB",
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
            InputBorder = "#FFC8C8C8",
            Text = "#FF212529",
            TextSecondary = "#FF6C757D",
            Border = "#FFDEE2E6",
            Success = "#FF009688",
            Warning = "#FFFF9800",
            Error = "#FFC62828",
            Info = "#FF004785",
            Sidebar = "#FF003366",
            SidebarText = "#FFE6E6E6",
            SidebarActive = "#FF009688",
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
            InputBorder = "#FFD1D5DB",
            Text = "#FF111827",
            TextSecondary = "#FF6B7280",
            Border = "#FFE5E7EB",
            Success = "#FF10B981",
            Warning = "#FFF59E0B",
            Error = "#FFEF4444",
            Info = "#FF6366F1",
            Sidebar = "#FF111827",
            SidebarText = "#FFD1D5DB",
            SidebarActive = "#FF6366F1",
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
            InputBorder = "#FF969696",
            Text = "#FFFFFFFF",
            TextSecondary = "#FFC8C8C8",
            Border = "#FF969696",
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
        Light, Dark, Professional, Modern, HighContrast,
    };

    public static List<AppTheme> GetAllLayoutPresets() => new()
    {
        Light, Spacious, Professional, Modern,
    };

    public static Dictionary<string, string> ThemeNameMap => new()
    {
        ["Light"] = "روشن",
        ["Dark"] = "تاریک",
        ["Professional"] = "حرفه‌ای",
        ["Modern"] = "مدرن",
        ["HighContrast"] = "کنتراست بالا",
        ["Spacious"] = "جادار",
    };
}
