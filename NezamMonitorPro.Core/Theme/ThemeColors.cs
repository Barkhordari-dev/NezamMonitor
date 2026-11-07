namespace NezamMonitorPro.Core.Theme;

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
    public string SidebarHover { get; set; } = "#1AFFFFFF";

    /// <summary>
    /// Soft, low-saturation background for the active sidebar item. Kept separate
    /// from <see cref="SidebarActive"/> so the active row can be a gentle tint
    /// (WCAG-friendly) instead of a loud full-saturation fill.
    /// </summary>
    public string SidebarActiveSoft { get; set; } = "#26FFFFFF";

    public string HoverOverlay { get; set; } = "#0F94A3B8";

    /// <summary>Very light grey used for alternating table rows (zebra striping).</summary>
    public string ZebraRow { get; set; } = "#FFF9FAFB";

    /// <summary>Very light tint shown when the mouse hovers a table row.</summary>
    public string TableRowHover { get; set; } = "#FFF1F5F9";

    /// <summary>Light tint used for the selected table row.</summary>
    public string TableRowSelected { get; set; } = "#FFE0E7FF";

    public string ShadowColor { get; set; } = "#FF0B1B33";

    /// <summary>Text colour to use ON a filled Primary/Success/... background.
    /// Derived by ThemeManager from the fill colour (white or near-black,
    /// whichever has the better WCAG contrast), so a bright fill in one theme
    /// never gets unreadable white text.</summary>
    public string OnPrimary { get; set; } = "#FFFFFFFF";
    public string OnAccent { get; set; } = "#FFFFFFFF";
    public string OnSuccess { get; set; } = "#FF111827";
    public string OnWarning { get; set; } = "#FF111827";
    public string OnError { get; set; } = "#FFFFFFFF";
    public string OnInfo { get; set; } = "#FFFFFFFF";
    public string OnSecondary { get; set; } = "#FFFFFFFF";

    /// <summary>Coloured text used ON a card/surface (e.g. dashboard numbers).
    /// A fill colour is often too light to read as text on a light card, or
    /// too dark on a dark card, so these are the fill colour mixed towards
    /// black (light card) or white (dark card).</summary>
    public string PrimaryText { get; set; } = "#FF2563EB";
    public string AccentText { get; set; } = "#FF7C3AED";
    public string SuccessText { get; set; } = "#FF047857";
    public string WarningText { get; set; } = "#FF92400E";
    public string ErrorText { get; set; } = "#FFB91C1C";

    public ThemeColors Clone()
    {
        return (ThemeColors)MemberwiseClone();
    }

    /// <summary>
    /// Recomputes <see cref="SidebarActiveSoft"/> from the current
    /// Sidebar / SidebarActive / SidebarText colours.
    /// The tint is blended over the sidebar background at the lowest alpha
    /// that still keeps the sidebar label readable (WCAG >= 7:1), so the
    /// active row is clearly marked without a loud saturated fill.
    /// </summary>
    public void DeriveSidebarActiveSoft()
    {
        var bg = ParseRgb(Sidebar);
        var accent = ParseRgb(SidebarActive);
        var text = ParseRgb(SidebarText);
        if (bg is null || accent is null || text is null) return;

        // Pick a clearly visible but still soft tint: the strongest alpha
        // within a tasteful cap that still keeps the label at WCAG >= 7:1.
        // (The earlier version picked the weakest passing alpha, which made
        // the active item essentially invisible.)
        (int R, int G, int B) chosen = default;
        var alpha = 0.0;
        var found = false;

        const double maxAlpha = 0.45;

        for (var a = (int)(maxAlpha * 100); a >= 8; a--)
        {
            var cand = Blend(accent.Value, bg.Value, a / 100.0);
            if (Contrast(text.Value, cand) >= 7.0)
            {
                chosen = cand;
                alpha = a / 100.0;
                found = true;
                break;
            }
        }

        if (!found)
        {
            // Sidebar text is very light: fall back to a white tint and take
            // the strongest one that still passes.
            for (var a = 39; a >= 4; a--)
            {
                var cand = Blend((255, 255, 255), bg.Value, a / 100.0);
                if (Contrast(text.Value, cand) >= 7.0)
                {
                    chosen = cand;
                    alpha = a / 100.0;
                    found = true;
                    break;
                }
            }
        }

        if (!found)
        {
            var cand = Blend(accent.Value, bg.Value, 0.12);
            chosen = cand;
            alpha = 0.12;
        }

        var byteAlpha = Math.Clamp((int)Math.Round(alpha * 255), 0x0A, 0xE0);
        SidebarActiveSoft = $"#{byteAlpha:X2}{chosen.R:X2}{chosen.G:X2}{chosen.B:X2}";
    }

    /// <summary>
    /// Derives the On* colours: for each filled colour, pick white or
    /// near-black text, whichever has the better WCAG contrast against it.
    /// </summary>
    public void DeriveOnColors()
    {
        OnPrimary = BestOn(Primary);
        OnAccent = BestOn(Accent);
        OnSuccess = BestOn(Success);
        OnWarning = BestOn(Warning);
        OnError = BestOn(Error);
        OnInfo = BestOn(Info);
        OnSecondary = BestOn(TextSecondary);
    }

    private static string BestOn(string hex)
    {
        var bg = ParseRgb(hex);
        if (bg is null) return "#FFFFFFFF";
        var white = (255, 255, 255);
        var dark = (17, 24, 39);
        return Contrast(white, bg.Value) >= Contrast(dark, bg.Value) ? "#FFFFFFFF" : "#FF111827";
    }

    /// <summary>
    /// Derives the *Text colours for coloured text on a card: mix the fill
    /// colour towards black on a light card, towards white on a dark card.
    /// </summary>
    public void DeriveTextColors()
    {
        var card = ParseRgb(Card);
        if (card is null) return;
        var lightCard = Luminance(card.Value) > 0.5;
        double mix = 0.45;
        PrimaryText = ToHex8(MixText(Primary, lightCard, mix));
        AccentText = ToHex8(MixText(Accent, lightCard, mix));
        SuccessText = ToHex8(MixText(Success, lightCard, mix));
        WarningText = ToHex8(MixText(Warning, lightCard, mix));
        ErrorText = ToHex8(MixText(Error, lightCard, mix));
    }

    private static (int R, int G, int B) MixText(string hex, bool lightCard, double mix)
    {
        var c = ParseRgb(hex) ?? (128, 128, 128);
        var towards = lightCard ? (0, 0, 0) : (255, 255, 255);
        return Blend(towards, c, mix);
    }

    private static string ToHex8((int R, int G, int B) c) => $"#FF{c.R:X2}{c.G:X2}{c.B:X2}";

    /// <summary>
    /// Recomputes the table tints from the theme's own background/panel
    /// colours so zebra striping, hover and selection stay subtle and
    /// coherent in every theme.
    /// </summary>
    public void DeriveTableTints()
    {
        var bg = ParseRgb(Background);
        var panel = ParseRgb(Panel);
        var primary = ParseRgb(Primary);
        if (bg is null || panel is null) return;

        // Zebra: a hair away from the background (very light grey).
        var zebra = Blend(panel.Value, bg.Value, 0.55);
        ZebraRow = ToHex(zebra);

        // Hover: a hair of the primary colour over the background.
        var hover = primary is null
            ? Blend(panel.Value, bg.Value, 0.35)
            : Blend(primary.Value, bg.Value, 0.07);
        TableRowHover = ToHex(hover);

        // Selected: a bit more of the primary so the row is clearly marked.
        var selected = primary is null
            ? Blend(panel.Value, bg.Value, 0.75)
            : Blend(primary.Value, bg.Value, 0.16);
        TableRowSelected = ToHex(selected);
    }

    private static string ToHex((int R, int G, int B) c)
        => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    private static (int R, int G, int B)? ParseRgb(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        var h = hex.Trim().TrimStart('#');
        if (h.Length == 8) h = h[2..];
        if (h.Length != 6) return null;
        try
        {
            return (
                Convert.ToInt32(h[..2], 16),
                Convert.ToInt32(h[2..4], 16),
                Convert.ToInt32(h[4..6], 16));
        }
        catch { return null; }
    }

    private static (int R, int G, int B) Blend((int R, int G, int B) fg, (int R, int G, int B) bg, double alpha)
        => (
            (int)Math.Round(fg.R * alpha + bg.R * (1 - alpha)),
            (int)Math.Round(fg.G * alpha + bg.G * (1 - alpha)),
            (int)Math.Round(fg.B * alpha + bg.B * (1 - alpha)));

    private static double Luminance((int R, int G, int B) c)
    {
        static double Lin(double v)
        {
            v /= 255.0;
            return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B);
    }

    private static double Contrast((int R, int G, int B) a, (int R, int G, int B) b)
    {
        var l1 = Luminance(a);
        var l2 = Luminance(b);
        var hi = Math.Max(l1, l2);
        var lo = Math.Min(l1, l2);
        return (hi + 0.05) / (lo + 0.05);
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
            ["SidebarActiveSoft"] = SidebarActiveSoft,
            ["LogBackground"] = LogBackground,
            ["SidebarHover"] = SidebarHover,
            ["HoverOverlay"] = HoverOverlay,
            ["ZebraRow"] = ZebraRow,
            ["TableRowHover"] = TableRowHover,
            ["TableRowSelected"] = TableRowSelected,
            ["ShadowColor"] = ShadowColor,
            ["OnPrimary"] = OnPrimary,
            ["OnAccent"] = OnAccent,
            ["OnSuccess"] = OnSuccess,
            ["OnWarning"] = OnWarning,
            ["OnError"] = OnError,
            ["OnInfo"] = OnInfo,
            ["OnSecondary"] = OnSecondary,
            ["PrimaryText"] = PrimaryText,
            ["AccentText"] = AccentText,
            ["SuccessText"] = SuccessText,
            ["WarningText"] = WarningText,
            ["ErrorText"] = ErrorText,
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
