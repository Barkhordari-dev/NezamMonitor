using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NezamMonitorPro.App.Services;
using NezamMonitorPro.App.ViewModels;

namespace NezamMonitorPro.App.Views;

public partial class DashboardView : UserControl, INavigationAware
{
    public DashboardView()
    {
        InitializeComponent();
        DataContext = new DashboardViewModel(DatabaseService.Instance);
        SyncGradientToTheme();
    }

    /// <summary>هنگام بازگشت به داشبورد، آمار دوباره از پایگاه‌داده خوانده می‌شود.</summary>
    public void OnNavigatedTo()
    {
        try
        {
            if (DataContext is DashboardViewModel vm)
                vm.RefreshCommand.Execute(null);
        }
        catch { }
    }

    /// <summary>
    /// GradientStop.Color cannot be bound to a Brush resource, so the two
    /// gradient colours are copied from the active theme at runtime.
    /// </summary>
    private void SyncGradientToTheme()
    {
        try
        {
            var app = Application.Current;
            if (app is null) return;

            if (app.TryFindResource("PrimaryBrush") is SolidColorBrush p)
                GradStart.Color = p.Color;
            if (app.TryFindResource("AccentBrush") is SolidColorBrush a)
                GradEnd.Color = a.Color;
        }
        catch { }
    }

    private void GoToUpdate_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = Window.GetWindow(this) as MainWindow;
        mainWindow?.NavigateTo("update");
    }

    private void GoToFollowUp_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = Window.GetWindow(this) as MainWindow;
        mainWindow?.NavigateTo("followup");
    }

    private void GoToExcel_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = Window.GetWindow(this) as MainWindow;
        mainWindow?.NavigateTo("excel");
    }

    private void GoToCases_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = Window.GetWindow(this) as MainWindow;
        mainWindow?.NavigateTo("cases");
    }

    private void GoToGenerator_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = Window.GetWindow(this) as MainWindow;
        mainWindow?.NavigateTo("generator");
    }
}
