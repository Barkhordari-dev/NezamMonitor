using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using NezamMonitorPro.App.ViewModels;

namespace NezamMonitorPro.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        // ARCH-001: ViewModel از کانتینر DI گرفته می‌شود.
        DataContext = App.Services.GetRequiredService<SettingsViewModel>();
    }

    private void AboutLink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = e.Uri.AbsoluteUri, UseShellExecute = true });
        }
        catch { }
        e.Handled = true;
    }

    // Hidden menu entry: 10 quick taps on the About title.
    // The counter resets if more than 3 seconds pass between taps.
    private int _aboutTapCount;
    private DateTime _lastAboutTapAt = DateTime.MinValue;

    private void AboutTitle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var now = DateTime.Now;
        _aboutTapCount = (now - _lastAboutTapAt).TotalSeconds > 3 ? 1 : _aboutTapCount + 1;
        _lastAboutTapAt = now;
        // S-018: از کلیک پنجم، عنوان کمی محو می‌شود تا کاربر بداند
        // کلیک‌ها دارند شمرده می‌شوند (نشانه خیلی ظریف).
        if (sender is TextBlock title)
            title.Opacity = _aboutTapCount >= 5 && _aboutTapCount < 10
                ? 1.0 - (_aboutTapCount - 4) * 0.06
                : 1.0;
        if (_aboutTapCount < 10) return;
        _aboutTapCount = 0;
        if (sender is TextBlock t) t.Opacity = 1.0;

        try
        {
            var passwordWindow = new HiddenPasswordWindow { Owner = Application.Current?.MainWindow };
            if (passwordWindow.ShowDialog() != true) return;
            var hiddenWindow = new HiddenSettingsWindow { Owner = Application.Current?.MainWindow };
            hiddenWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            // باز نشدن منوی مخفی نباید برنامه را با خطای سراسری ببندد؛
            // علتش را همین‌جا نشان بده تا کاربر بداند چه شده.
            MessageBox.Show("منوی مخفی باز نشد: " + ex.Message, "منوی مخفی",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // S-011: انتخاب‌ها همان لحظه اعمال می‌شوند (پیش‌نمایش زنده)؛
    // دکمه‌های «اعمال» حذف شدند.
    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm && IsLoaded)
            vm.ApplyTheme();
    }

    private void ButtonStyleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm && IsLoaded)
            vm.ApplyButtonStyle();
    }

    private void FontFamilyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm && IsLoaded)
            vm.ApplyFonts();
    }

    private void TableAlignmentCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm && IsLoaded)
            vm.ApplyTableAlignment();
    }

    private void FontBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm || !IsLoaded) return;
        // اول مقدار تایپ‌شده را به ViewModel بده، بعد اعمال کن.
        if (sender is TextBox tb)
            tb.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        vm.ApplyFonts();
    }
}
