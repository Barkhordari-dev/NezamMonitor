using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using NezamMonitorPro.Core.Data;

namespace NezamMonitorPro.App.Services;

/// <summary>
/// Button look presets ("حالت دکمه‌ها" in Settings, under the theme picker).
/// The button templates in ProTheme.xaml read five tokens with
/// DynamicResource (ButtonCornerRadius, ButtonOverlayBrush,
/// ButtonFrameBrush, ButtonFrameThickness, ButtonShadowEffect), so
/// applying a preset updates every button live. The choice is saved
/// as theme_ButtonStyle and re-applied at startup from MainWindow.
/// </summary>
public static class ButtonStyleManager
{
    public const string SettingKey = SettingKeys.ThemeButtonStyle;
    public const string DefaultKey = "classic";

    public sealed record Preset(
        string Key,
        string DisplayName,
        CornerRadius Radius,
        Brush Overlay,
        Brush Frame,
        Thickness FrameThickness,
        Effect? Shadow);

    private static LinearGradientBrush WhiteGradient(byte topAlpha, byte bottomAlpha)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1)
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(topAlpha, 0xFF, 0xFF, 0xFF), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(bottomAlpha, 0xFF, 0xFF, 0xFF), 1));
        return brush;
    }

    public static readonly IReadOnlyList<Preset> Presets = new[]
    {
        new Preset("classic", "کلاسیک", new CornerRadius(8),
            Brushes.Transparent, Brushes.Transparent, new Thickness(0), null),
        new Preset("glass", "شیشه‌ای", new CornerRadius(12),
            WhiteGradient(0x66, 0x1A),
            new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF)), new Thickness(1),
            new DropShadowEffect { BlurRadius = 10, ShadowDepth = 1, Direction = 270, Opacity = 0.25, Color = Colors.Black }),
        new Preset("pill", "گرد", new CornerRadius(19),
            Brushes.Transparent, Brushes.Transparent, new Thickness(0), null),
        new Preset("flat", "تخت", new CornerRadius(0),
            Brushes.Transparent, Brushes.Transparent, new Thickness(0), null),
        new Preset("gradient", "گرادیان", new CornerRadius(8),
            WhiteGradient(0x4D, 0x00),
            Brushes.Transparent, new Thickness(0), null),
        new Preset("elevated", "سایه‌دار", new CornerRadius(10),
            Brushes.Transparent, Brushes.Transparent, new Thickness(0),
            new DropShadowEffect { BlurRadius = 12, ShadowDepth = 3, Direction = 270, Opacity = 0.4, Color = Colors.Black }),
    };

    public static Preset Find(string key)
        => Presets.FirstOrDefault(p => p.Key == key) ?? Presets[0];

    public static string Load(NezamDatabase db)
        => db.GetSetting(SettingKey, DefaultKey);

    public static void Apply(string key, NezamDatabase db, bool save = true)
    {
        var preset = Find(key);
        var app = Application.Current;
        if (app != null)
        {
            app.Resources["ButtonCornerRadius"] = preset.Radius;
            app.Resources["ButtonOverlayBrush"] = preset.Overlay;
            app.Resources["ButtonFrameBrush"] = preset.Frame;
            app.Resources["ButtonFrameThickness"] = preset.FrameThickness;
            // A null assignment removes the key; the DynamicResource lookup
            // then yields null, which means "no shadow" for the templates.
            app.Resources["ButtonShadowEffect"] = preset.Shadow;
        }
        if (save)
            db.SaveSetting(SettingKey, preset.Key);
    }

    public static void ApplySaved(NezamDatabase db)
        => Apply(Load(db), db, save: false);
}
