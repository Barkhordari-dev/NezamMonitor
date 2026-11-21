using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using NezamMonitorPro.App.Services;
using NezamMonitorPro.App.ViewModels;

namespace NezamMonitorPro.App.Views;

/// <summary>پنجره مودال پروفایل مهندس. با نام کامل باز می‌شود؛ کلیک روی نام
/// دفتر، جزییات آن دفتر را در پنجره‌ای تازه باز می‌کند.</summary>
public partial class DirectoryEngineerProfileWindow : Window
{
    private readonly DirectoryEngineerProfileViewModel _vm;

    public DirectoryEngineerProfileWindow(string engineerName)
    {
        InitializeComponent();
        _vm = new DirectoryEngineerProfileViewModel(
            App.Services.GetRequiredService<DirectoryService>(), engineerName);
        DataContext = _vm;
        Loaded += async (_, _) => await _vm.LoadAsync();
    }

    private void Photo_ImageFailed(object sender, ExceptionRoutedEventArgs e)
    {
        // عکس خراب/نرسیده = حروف اول نام می‌ماند.
        _vm.HidePhoto();
    }

    private void Office_Click(object sender, MouseButtonEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_vm.OfficeName) && _vm.OfficeName != "ثبت نشده")
        {
            try
            {
                new DirectoryOfficeDetailWindow(_vm.OfficeName) { Owner = this }.ShowDialog();
            }
            catch { }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
