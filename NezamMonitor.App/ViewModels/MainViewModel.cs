namespace NezamMonitor.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private string _title = "سیستم مانیتور نظم مهندسی یزد";
    public string Title { get => _title; set => SetProperty(ref _title, value); }

    private string _currentView = "dashboard";
    public string CurrentView { get => _currentView; set => SetProperty(ref _currentView, value); }

    private string _statusMessage = "آماده";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

    private double _progress;
    public double Progress { get => _progress; set => SetProperty(ref _progress, value); }

    private string _currentActivity = "";
    public string CurrentActivity { get => _currentActivity; set => SetProperty(ref _currentActivity, value); }
}
