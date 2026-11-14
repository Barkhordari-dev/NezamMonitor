using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NezamMonitorPro.App.Services;
using NezamMonitorPro.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace NezamMonitorPro.App.Views;

public partial class DashboardView : UserControl, INavigationAware
{
    public DashboardView()
    {
        InitializeComponent();
        // ARCH-001: ViewModel از کانتینر DI گرفته می‌شود.
        DataContext = App.Services.GetRequiredService<DashboardViewModel>();
        SyncGradientToTheme();
        SizeChanged += DashboardView_SizeChanged;
    }

    /// <summary>
    /// L-009/L-010/L-011: در عرض باریک (پنجره ۱۰۲۴ یا سایدبار باز)،
    /// کارت‌های آماری ۲ ستونه می‌شوند، پنل گرادیانی باریک می‌شود و
    /// دو ستون بدنه روی هم چیده می‌شوند.
    /// </summary>
    private void DashboardView_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        try
        {
            bool narrow = ActualWidth < 900;

            // L-009: کارت‌های آماری ۴ ستونه → ۲ ستونه (حاشیه‌های اصلی حفظ می‌شوند).
            StatGrid.Columns = narrow ? 2 : 4;
            for (int i = 0; i < StatGrid.Children.Count; i++)
            {
                if (StatGrid.Children[i] is not Border tile) continue;
                tile.Margin = narrow
                    ? (i % 2 == 0 ? new Thickness(0, 0, 6, 12) : new Thickness(6, 0, 0, 12))
                    : i switch
                    {
                        0 => new Thickness(0, 0, 12, 0),
                        1 => new Thickness(4, 0, 8, 0),
                        2 => new Thickness(8, 0, 4, 0),
                        _ => new Thickness(12, 0, 0, 0),
                    };
            }

            // L-010: پنل تزیینی هیرو ۲۶۰ → ۱۸۰.
            if (HeroPanel != null) HeroPanel.Width = narrow ? 180 : 260;

            // L-011: بدنه دو ستونه → روی هم (کارت دسترسی سریع زیر کارت داده).
            if (narrow)
            {
                BodyGrid.ColumnDefinitions.Clear();
                BodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                BodyGrid.RowDefinitions.Clear();
                BodyGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                BodyGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetColumn(DataCard, 0);
                Grid.SetRow(DataCard, 0);
                Grid.SetColumn(QuickCard, 0);
                Grid.SetRow(QuickCard, 1);
                QuickCard.Margin = new Thickness(0, 14, 0, 0);
            }
            else
            {
                BodyGrid.ColumnDefinitions.Clear();
                BodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                BodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
                BodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });
                BodyGrid.RowDefinitions.Clear();
                BodyGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetColumn(DataCard, 0);
                Grid.SetRow(DataCard, 0);
                Grid.SetColumn(QuickCard, 2);
                Grid.SetRow(QuickCard, 0);
                QuickCard.Margin = new Thickness(0);
            }
        }
        catch { }
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
