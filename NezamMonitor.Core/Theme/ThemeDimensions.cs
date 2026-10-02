namespace NezamMonitor.Core.Theme;

/// <summary>
/// Defines all customizable UI fonts and font sizes as strings/doubles.
/// No WPF dependency in Core layer.
/// </summary>
public sealed class ThemeFonts
{
    public string FontFamily { get; set; } = "B Nazanin";
    public string HeadingFontFamily { get; set; } = "B Nazanin";
    public double GeneralFontSize { get; set; } = 14;
    public double HeadingFontSize { get; set; } = 20;
    public double ButtonFontSize { get; set; } = 13;
    public double InputFontSize { get; set; } = 13;
    public double LabelFontSize { get; set; } = 13;
    public double LogFontSize { get; set; } = 11;
    public double SidebarFontSize { get; set; } = 14;

    public ThemeFonts Clone() => (ThemeFonts)MemberwiseClone();

    public Dictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string>
        {
            ["FontFamily"] = FontFamily,
            ["HeadingFontFamily"] = HeadingFontFamily,
            ["GeneralFontSize"] = GeneralFontSize.ToString(),
            ["HeadingFontSize"] = HeadingFontSize.ToString(),
            ["ButtonFontSize"] = ButtonFontSize.ToString(),
            ["InputFontSize"] = InputFontSize.ToString(),
            ["LabelFontSize"] = LabelFontSize.ToString(),
            ["LogFontSize"] = LogFontSize.ToString(),
            ["SidebarFontSize"] = SidebarFontSize.ToString(),
        };
    }

    public static ThemeFonts FromDictionary(Dictionary<string, string> dict)
    {
        var tf = new ThemeFonts();
        if (dict.TryGetValue("FontFamily", out var ff)) tf.FontFamily = ff;
        if (dict.TryGetValue("HeadingFontFamily", out var hf)) tf.HeadingFontFamily = hf;
        if (dict.TryGetValue("GeneralFontSize", out var gs) && double.TryParse(gs, out var gsVal)) tf.GeneralFontSize = gsVal;
        if (dict.TryGetValue("HeadingFontSize", out var hs) && double.TryParse(hs, out var hsVal)) tf.HeadingFontSize = hsVal;
        if (dict.TryGetValue("ButtonFontSize", out var bs) && double.TryParse(bs, out var bsVal)) tf.ButtonFontSize = bsVal;
        if (dict.TryGetValue("InputFontSize", out var is2) && double.TryParse(is2, out var isVal)) tf.InputFontSize = isVal;
        if (dict.TryGetValue("LabelFontSize", out var ls) && double.TryParse(ls, out var lsVal)) tf.LabelFontSize = lsVal;
        if (dict.TryGetValue("LogFontSize", out var lg) && double.TryParse(lg, out var lgVal)) tf.LogFontSize = lgVal;
        if (dict.TryGetValue("SidebarFontSize", out var ss) && double.TryParse(ss, out var ssVal)) tf.SidebarFontSize = ssVal;
        return tf;
    }
}

/// <summary>
/// Defines all customizable UI dimensions as doubles.
/// No WPF dependency in Core layer.
/// </summary>
public sealed class ThemeDimensions
{
    public double WindowWidth { get; set; } = 1200;
    public double WindowHeight { get; set; } = 750;
    public double MinWindowWidth { get; set; } = 800;
    public double MinWindowHeight { get; set; } = 500;
    public double ButtonWidth { get; set; } = 180;
    public double ButtonHeight { get; set; } = 40;
    public double InputHeight { get; set; } = 32;
    public double BorderRadius { get; set; } = 6;
    public double Padding { get; set; } = 16;
    public double Margin { get; set; } = 8;
    public double ControlSpacing { get; set; } = 8;
    public double SidebarWidth { get; set; } = 200;
    public double HeaderHeight { get; set; } = 50;

    public ThemeDimensions Clone() => (ThemeDimensions)MemberwiseClone();

    public Dictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string>
        {
            ["WindowWidth"] = WindowWidth.ToString(),
            ["WindowHeight"] = WindowHeight.ToString(),
            ["MinWindowWidth"] = MinWindowWidth.ToString(),
            ["MinWindowHeight"] = MinWindowHeight.ToString(),
            ["ButtonWidth"] = ButtonWidth.ToString(),
            ["ButtonHeight"] = ButtonHeight.ToString(),
            ["InputHeight"] = InputHeight.ToString(),
            ["BorderRadius"] = BorderRadius.ToString(),
            ["Padding"] = Padding.ToString(),
            ["Margin"] = Margin.ToString(),
            ["ControlSpacing"] = ControlSpacing.ToString(),
            ["SidebarWidth"] = SidebarWidth.ToString(),
            ["HeaderHeight"] = HeaderHeight.ToString(),
        };
    }

    public static ThemeDimensions FromDictionary(Dictionary<string, string> dict)
    {
        var td = new ThemeDimensions();
        foreach (var prop in typeof(ThemeDimensions).GetProperties())
        {
            if (dict.TryGetValue(prop.Name, out var val) && double.TryParse(val, out var d))
                prop.SetValue(td, d);
        }
        return td;
    }
}

/// <summary>
/// Layout direction configuration (RTL/LTR for different UI elements).
/// </summary>
public sealed class LayoutDirection
{
    public string TextDirection { get; set; } = "RightToLeft"; // RightToLeft, LeftToRight
    public string TableDirection { get; set; } = "RightToLeft"; // RightToLeft, LeftToRight
    public string MenuDirection { get; set; } = "RightToLeft"; // RightToLeft, LeftToRight
    public string MainWindowDirection { get; set; } = "RightToLeft"; // RightToLeft, LeftToRight

    public LayoutDirection Clone() => (LayoutDirection)MemberwiseClone();

    public Dictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string>
        {
            ["TextDirection"] = TextDirection,
            ["TableDirection"] = TableDirection,
            ["MenuDirection"] = MenuDirection,
            ["MainWindowDirection"] = MainWindowDirection,
        };
    }

    public static LayoutDirection FromDictionary(Dictionary<string, string> dict)
    {
        var ld = new LayoutDirection();
        if (dict.TryGetValue("TextDirection", out var td)) ld.TextDirection = td;
        if (dict.TryGetValue("TableDirection", out var tbl)) ld.TableDirection = tbl;
        if (dict.TryGetValue("MenuDirection", out var md)) ld.MenuDirection = md;
        if (dict.TryGetValue("MainWindowDirection", out var mwd)) ld.MainWindowDirection = mwd;
        return ld;
    }
}

/// <summary>
/// Button layout configuration.
/// </summary>
public sealed class ButtonLayout
{
    public string Alignment { get; set; } = "Left"; // Left, Center, Right
    public string Direction { get; set; } = "Vertical"; // Horizontal, Vertical
    public double Spacing { get; set; } = 10;

    public ButtonLayout Clone() => (ButtonLayout)MemberwiseClone();

    public Dictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string>
        {
            ["Alignment"] = Alignment,
            ["Direction"] = Direction,
            ["Spacing"] = Spacing.ToString(),
        };
    }

    public static ButtonLayout FromDictionary(Dictionary<string, string> dict)
    {
        var bl = new ButtonLayout();
        if (dict.TryGetValue("Alignment", out var a)) bl.Alignment = a;
        if (dict.TryGetValue("Direction", out var d)) bl.Direction = d;
        if (dict.TryGetValue("Spacing", out var s) && double.TryParse(s, out var sv)) bl.Spacing = sv;
        return bl;
    }
}