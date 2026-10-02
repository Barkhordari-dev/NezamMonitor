namespace NezamMonitor.Core.Theme;

/// <summary>
/// Defines all customizable UI colors as hex strings.
/// No WPF dependency in Core layer.
/// </summary>
public sealed class ThemeColors
{
    public string Primary { get; set; } = "#FF3498DB";
    public string Secondary { get; set; } = "#FF2ECC71";
    public string Accent { get; set; } = "#FF9B59B6";
    public string Background { get; set; } = "#FFF5F5F5";
    public string Surface { get; set; } = "#FFFFFFFF";
    public string Panel { get; set; } = "#FFECEFF1";
    public string Card { get; set; } = "#FFFFFFFF";
    public string ButtonPrimary { get; set; } = "#FF3498DB";
    public string ButtonPrimaryHover { get; set; } = "#FF2980B9";
    public string ButtonPrimaryPressed { get; set; } = "#FF246EA0";
    public string ButtonDisabled { get; set; } = "#FFBDC3C7";
    public string InputBackground { get; set; } = "#FFFFFFFF";
    public string InputBorder { get; set; } = "#FFBDC3C7";
    public string Text { get; set; } = "#FF2C3E50";
    public string TextSecondary { get; set; } = "#FF7F8C8D";
    public string Border { get; set; } = "#FFBDC3C7";
    public string Success { get; set; } = "#FF2ECC71";
    public string Warning { get; set; } = "#FFF1C40F";
    public string Error { get; set; } = "#FFE74C3C";
    public string Info { get; set; } = "#FF3498DB";
    public string Sidebar { get; set; } = "#FF2C3E50";
    public string SidebarText { get; set; } = "#FFFFFFFF";
    public string SidebarActive { get; set; } = "#FF3498DB";
    public string LogBackground { get; set; } = "#FF2C3E50";

    public ThemeColors Clone()
    {
        return (ThemeColors)MemberwiseClone();
    }

    public Dictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string>
        {
            ["Primary"] = Primary,
            ["Secondary"] = Secondary,
            ["Accent"] = Accent,
            ["Background"] = Background,
            ["Surface"] = Surface,
            ["Panel"] = Panel,
            ["Card"] = Card,
            ["ButtonPrimary"] = ButtonPrimary,
            ["ButtonPrimaryHover"] = ButtonPrimaryHover,
            ["ButtonPrimaryPressed"] = ButtonPrimaryPressed,
            ["ButtonDisabled"] = ButtonDisabled,
            ["InputBackground"] = InputBackground,
            ["InputBorder"] = InputBorder,
            ["Text"] = Text,
            ["TextSecondary"] = TextSecondary,
            ["Border"] = Border,
            ["Success"] = Success,
            ["Warning"] = Warning,
            ["Error"] = Error,
            ["Info"] = Info,
            ["Sidebar"] = Sidebar,
            ["SidebarText"] = SidebarText,
            ["SidebarActive"] = SidebarActive,
            ["LogBackground"] = LogBackground,
        };
    }

    public static ThemeColors FromDictionary(Dictionary<string, string> dict)
    {
        var tc = new ThemeColors();
        foreach (var prop in typeof(ThemeColors).GetProperties())
        {
            if (dict.TryGetValue(prop.Name, out var val) && val.StartsWith("#"))
                prop.SetValue(tc, val);
        }
        return tc;
    }
}
