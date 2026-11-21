using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using NezamMonitorPro.App.Services;
using NezamMonitorPro.App.ViewModels;

namespace NezamMonitorPro.App.Views;

/// <summary>پنجره مودال جزییات دفتر. با متن خام دفتر از پرونده باز می‌شود؛
/// کلیک روی هر عضو، پروفایل آن مهندس را در پنجره‌ای تازه باز می‌کند.</summary>
public partial class DirectoryOfficeDetailWindow : Window
{
    private readonly DirectoryOfficeDetailViewModel _vm;

    public DirectoryOfficeDetailWindow(string rawOfficeText)
    {
        InitializeComponent();
        _vm = new DirectoryOfficeDetailViewModel(
            App.Services.GetRequiredService<DirectoryService>(), rawOfficeText);
        DataContext = _vm;
        Loaded += async (_, _) => await _vm.LoadAsync();
    }

    private void Member_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBlock tb && tb.Tag is string name && !string.IsNullOrWhiteSpace(name))
        {
            try
            {
                new DirectoryEngineerProfileWindow(name) { Owner = this }.ShowDialog();
            }
            catch { }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
