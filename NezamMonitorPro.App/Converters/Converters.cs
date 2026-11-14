using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace NezamMonitorPro.App.Converters;
/// <summary>
/// Converts bool to Visibility (true = Visible, false = Collapsed).
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility v && v == Visibility.Visible;
    }
}

/// <summary>
/// Inverts a boolean value.
/// </summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b) return !b;
        return false;
    }
}

/// <summary>
/// Converts non-empty string to Visibility.
/// </summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s && !string.IsNullOrEmpty(s))
            return Visibility.Visible;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

/// <summary>
/// Converts bool to arrow character (▲/▼).
/// </summary>
public sealed class BoolToArrowConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? "▲" : "▼";
        return "▼";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

/// <summary>
/// Converts integer stage to bool (for RadioButton IsChecked).
/// </summary>
public sealed class StageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int stage && parameter is string s && int.TryParse(s, out int param))
            return stage == param;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter is string s && int.TryParse(s, out int param))
            return param;
        return 0;
    }
}

/// <summary>
/// Converts int tab index to Visibility (0 = Visible, else Collapsed).
/// </summary>
public sealed class TabToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int activeTab && parameter is string s && int.TryParse(s, out int tabIndex))
            return activeTab == tabIndex ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>
/// Converts int tab index to background color for tab buttons.
/// </summary>
public sealed class TabBgConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Theme-aware: active tab uses the theme Primary, inactive uses Panel
        // (was hard-coded #3498DB / #ECF0F1, wrong in dark themes).
        var active = value is int activeTab && parameter is string s && int.TryParse(s, out int tabIndex) && activeTab == tabIndex;
        return Application.Current?.TryFindResource(active ? "PrimaryBrush" : "PanelBrush") ?? Brushes.LightGray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>
/// Converts int tab index to foreground color for tab buttons.
/// </summary>
public sealed class TabFgConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var active = value is int activeTab && parameter is string s && int.TryParse(s, out int tabIndex) && activeTab == tabIndex;
        return Application.Current?.TryFindResource(active ? "OnPrimaryBrush" : "TextBrush") ?? Brushes.Black;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>
/// Converts int to bool for report count > 1 (for styling reports with multiple entries).
/// Used by FollowUpView to highlight cases with more than one report.
/// </summary>
public sealed class ReportCountConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
            return count > 1;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

// ═══════════════════════════════════════════════════════
// Dashboard Converters
// ═══════════════════════════════════════════════════════

/// <summary>Converts status level string to brush for left-side indicator bar.
/// T-011: رنگ‌ها از براش‌های تم می‌آیند (نه هگز سخت‌کدشده) تا در تم تیره هم درست باشند.</summary>
public sealed class LevelToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "critical" => ThemeBrush("ErrorBrush", "#E74C3C"),
            "warning" => ThemeBrush("WarningBrush", "#E67E22"),
            "urgent" => ThemeBrush("WarningBrush", "#F39C12"),
            "normal" => ThemeBrush("InfoBrush", "#3498DB"),
            _ => ThemeBrush("TextSecondaryBrush", "#BDC3C7")
        };
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;

    /// <summary>براش تم؛ اگر برنامه هنوز بالا نیامده باشد، همان رنگ قبلی به‌صورت هگز.</summary>
    internal static object ThemeBrush(string key, string fallbackHex)
    {
        if (Application.Current?.TryFindResource(key) is Brush b) return b;
        return fallbackHex;
    }
}

/// <summary>Converts status level string to background brush for status badge.</summary>
public sealed class LevelToBgBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "urgent" => LevelToBrushConverter.ThemeBrush("ErrorBrush", "#E74C3C"),
            "normal" => LevelToBrushConverter.ThemeBrush("InfoBrush", "#3498DB"),
            _ => LevelToBrushConverter.ThemeBrush("InfoBrush", "#3498DB")
        };
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Converts int: 0 → Gray, else → default (Black).</summary>
public sealed class ZeroToGrayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int i && i == 0) return LevelToBrushConverter.ThemeBrush("SuccessBrush", "#27AE60");
        return LevelToBrushConverter.ThemeBrush("ErrorBrush", "#E74C3C");
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Converts int: 0 → Collapsed, else → Visible.</summary>
public sealed class ZeroToVisibleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int i && i == 0) return Visibility.Collapsed;
        return Visibility.Visible;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>Converts int: >0 → Visible (badge), 0 → Collapsed.</summary>
public sealed class CountToBadgeVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int i && i > 0) return Visibility.Visible;
        return Visibility.Collapsed;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>
/// Attached property supplying the hint text drawn inside an empty TextBox.
/// Replaces the ToolTip-only placeholders so the hint is actually visible
/// and readable inside the field.
/// </summary>
public static class Watermark
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.RegisterAttached(
            "Text", typeof(string), typeof(Watermark),
            new PropertyMetadata("", OnTextChanged));

    public static void SetText(DependencyObject d, string value) => d.SetValue(TextProperty, value);
    public static string GetText(DependencyObject d) => (string)d.GetValue(TextProperty);

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox tb) return;
        if (e.NewValue is string s && !string.IsNullOrEmpty(s))
        {
            if (tb.Tag is not Grid) tb.Tag = BuildWatermark(tb, s);
        }
        else if (tb.Tag is Grid g)
        {
            g.Visibility = Visibility.Collapsed;
        }
    }

    private static Grid BuildWatermark(TextBox tb, string text)
    {
        var g = new Grid { IsHitTestVisible = false };
        tb.TextChanged += (_, _) =>
            g.Visibility = string.IsNullOrEmpty(tb.Text) ? Visibility.Visible : Visibility.Collapsed;

        g.Children.Add(new TextBlock
        {
            Text = text,
            Foreground = System.Windows.Media.Brushes.Gray,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            FontSize = tb.FontSize,
            FontFamily = tb.FontFamily,
        });

        g.Visibility = string.IsNullOrEmpty(tb.Text) ? Visibility.Visible : Visibility.Collapsed;
        return g;
    }
}

/// <summary>
/// Converts numeric string or long to "N0" formatted string (e.g., 1,234,567).
/// </summary>
public sealed class CurrencyStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        string s = value?.ToString() ?? string.Empty;
        if (long.TryParse(s.Replace(",", ""), out long val))
            return val.ToString("N0", culture);
        return string.IsNullOrEmpty(s) ? "0" : s;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
