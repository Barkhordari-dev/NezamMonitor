using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using NezamMonitorPro.App.ViewModels;

namespace NezamMonitorPro.App.Views;

/// <summary>Hidden settings window. Only opened after the password unlock;
/// the unlock itself is session-only (HiddenMenuSession).</summary>
public partial class HiddenSettingsWindow : Window
{
    public HiddenSettingsWindow()
    {
        InitializeComponent();
        // ARCH-001: ViewModel از کانتینر DI گرفته می‌شود.
        var vm = App.Services.GetRequiredService<HiddenSettingsViewModel>();
        vm.RequestClose += Close;
        DataContext = vm;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // A11Y-003: Esc پنجره را می‌بندد.
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }
}
