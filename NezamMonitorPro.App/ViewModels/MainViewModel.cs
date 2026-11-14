using System.Windows;
using System.Windows.Input;

namespace NezamMonitorPro.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private string _title = "NezamMonitor Pro — مانیتورینگ نظام مهندسی";
    public string Title { get => _title; set => SetProperty(ref _title, value); }

    private string _currentView = "dashboard";
    public string CurrentView { get => _currentView; set => SetProperty(ref _currentView, value); }

    private string _statusMessage = "آماده — نسخه پرو";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

    public MainViewModel()
    {
        // B-011: F5 صفحه جاری را تازه می‌کند (نه فقط یک پیام).
        RefreshCommand = new RelayCommand(() =>
        {
            if (Application.Current?.MainWindow is MainWindow mw) mw.RefreshCurrentPage();
        });
        GoToDashboardCommand = new RelayCommand(() => Navigate("dashboard"));
        GoToUpdateCommand = new RelayCommand(() => Navigate("update"));
        GoToGeneratorCommand = new RelayCommand(() => Navigate("generator"));
    }

    public ICommand RefreshCommand { get; }
    public ICommand GoToDashboardCommand { get; }
    public ICommand GoToUpdateCommand { get; }
    public ICommand GoToGeneratorCommand { get; }

    private static void Navigate(string key)
    {
        if (Application.Current?.MainWindow is MainWindow mw) mw.NavigateTo(key);
    }
}
