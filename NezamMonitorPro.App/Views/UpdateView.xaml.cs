using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using NezamMonitorPro.App.Services;
using NezamMonitorPro.App.ViewModels;

namespace NezamMonitorPro.App.Views;

public partial class UpdateView : UserControl
{
    private readonly UpdateViewModel _vm;
    private readonly DispatcherTimer _uiTimer;
    private Storyboard? _pulseSb;
    private Storyboard? _floatSb;
    private Storyboard? _spinSb;
    private Storyboard? _popSb;
    private Storyboard? _popSb2;
    private Storyboard? _slideSb;
    private bool _initialized;

    public UpdateView()
    {
        InitializeComponent();
        _vm = new UpdateViewModel(DatabaseService.Instance);
        DataContext = _vm;

        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _uiTimer.Tick += (s, e) => UpdateButtonStates();

        Loaded += (_, _) =>
        {
            UpdateButtonStates();
            if (!_initialized)
            {
                _initialized = true;
                ApplyStageVisual(ExtractionController.Instance.Stage, animate: false);
            }
        };

        Unloaded += (_, _) => _uiTimer.Stop();

        ExtractionController.Instance.PropertyChanged += UpdateView_PropertyChanged;
    }

    private void UpdateView_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ExtractionController.IsBusy))
        {
            if (ExtractionController.Instance.IsBusy)
            {
                _uiTimer.Start();
                UpdateButtonStates();
            }
            else
            {
                _uiTimer.Stop();
                UpdateButtonStates();
            }
        }
        else if (e.PropertyName == nameof(ExtractionController.Stage))
        {
            ApplyStageVisual(ExtractionController.Instance.Stage, animate: true);
        }
        else if (e.PropertyName == nameof(ExtractionController.FriendlyStatus))
        {
            StatusTitle.Text = ExtractionController.Instance.FriendlyStatus;
            SlideIn();
        }
        else if (e.PropertyName == nameof(ExtractionController.StatusDetail))
        {
            StatusDetail.Text = ExtractionController.Instance.StatusDetail;
        }
        else if (e.PropertyName == nameof(ExtractionController.ProgressCountText))
        {
            var txt = ExtractionController.Instance.ProgressCountText;
            ProgressCount.Text = txt;
            BigCount.Text = txt;
            CountBadge.Visibility = string.IsNullOrEmpty(txt) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  Stage-driven visuals
    // ══════════════════════════════════════════════════════════════
    private void ApplyStageVisual(ExtractionController.UpdateStage stage, bool animate)
    {
        StopAnimations();

        bool busy = stage == ExtractionController.UpdateStage.Connecting
                 || stage == ExtractionController.UpdateStage.Receiving
                 || stage == ExtractionController.UpdateStage.Saving
                 || stage == ExtractionController.UpdateStage.Downloading;
        bool success = stage == ExtractionController.UpdateStage.Success;
        bool error = stage == ExtractionController.UpdateStage.Error;

        SpinRing.Visibility = stage == ExtractionController.UpdateStage.Connecting ? Visibility.Visible : Visibility.Collapsed;
        CloudIcon.Visibility = busy && stage != ExtractionController.UpdateStage.Connecting ? Visibility.Visible : Visibility.Collapsed;
        SuccessTick.Visibility = success ? Visibility.Visible : Visibility.Collapsed;

        if (error)
            IconCircle.Background = new SolidColorBrush(Color.FromArgb(0x1A, 0xEF, 0x44, 0x44));
        else if (success)
            IconCircle.Background = new SolidColorBrush(Color.FromArgb(0x1A, 0x10, 0xB9, 0x81));
        else
            IconCircle.Background = (Brush)FindResource("PanelBrush");

        ProgressRow.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        if (busy && !success)
        {
            ProgressFill.Fill = (Brush)FindResource("PrimaryBrush");
        }

        if (success)
        {
            ProgressFill.Fill = (Brush)FindResource("SuccessBrush");
            if (animate)
            {
                _popSb = (Storyboard)FindResource("PopScaleXStory");
                _popSb.Begin(SuccessTick);
                _popSb2 = (Storyboard)FindResource("PopScaleYStory");
                _popSb2.Begin(SuccessTick);
            }
            else
            {
                SuccessScale.ScaleX = 1;
                SuccessScale.ScaleY = 1;
            }
        }

        if (stage == ExtractionController.UpdateStage.Connecting)
        {
            _spinSb = (Storyboard)FindResource("SpinStory");
            _spinSb.Begin(SpinRing);
        }
        else if (busy)
        {
            _pulseSb = (Storyboard)FindResource("PulseStory");
            _pulseSb.Begin(CloudIcon);

            _floatSb = (Storyboard)FindResource("FloatStory");
            _floatSb.Begin(ArrowGroup);
        }

        if (animate) SlideIn();
    }

    private void StopAnimations()
    {
        foreach (var sb in new Storyboard?[] { _pulseSb, _floatSb, _spinSb, _popSb, _popSb2 })
        {
            try { sb?.Stop(); } catch { }
        }
        _pulseSb = _floatSb = _spinSb = _popSb = _popSb2 = null;
    }

    private void SlideIn()
    {
        try
        {
            _slideSb = (Storyboard)FindResource("SlideMoveStory");
            _slideSb.Begin(StatusCard);
        }
        catch { }
    }

    private void UpdateButtonStates()
    {
        try
        {
            var ec = ExtractionController.Instance;
            if (ec == null) return;

            if (BtnStart != null) BtnStart.IsEnabled = !ec.IsBusy;
            if (BtnStop != null) BtnStop.IsEnabled = ec.IsBusy;
            if (BtnLoad != null) BtnLoad.IsEnabled = !ec.IsBusy;

            if (ProgressFill != null)
            {
                double pct = Math.Max(0, Math.Min(100, ec.Progress));
                ProgressFill.Tag = pct;
                UpdateProgressWidth();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateButtonStates error: {ex.Message}");
        }
    }

    private void UpdateProgressWidth()
    {
        try
        {
            if (ProgressFill == null || ProgressTrack == null) return;
            double pct = ProgressFill.Tag is double d ? d : 0;
            double w = ProgressTrack.ActualWidth;
            if (w <= 0) return;
            ProgressFill.Width = Math.Max(0, w * pct / 100.0);
        }
        catch { }
    }

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        var ec = ExtractionController.Instance;
        ec.Username = DataContext is ViewModels.UpdateViewModel vm ? vm.Username : "";
        ec.Password = DataContext is ViewModels.UpdateViewModel vm2 ? vm2.Password : "";
        ec.RememberMe = DataContext is ViewModels.UpdateViewModel vm3 ? vm3.RememberMe : false;

        if (ec.StartCommand is ViewModels.AsyncRelayCommand asyncCmd)
            await asyncCmd.ExecuteAsync();
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        ExtractionController.Instance.StopCommand.Execute(null);
    }

    private void BtnLoad_Click(object sender, RoutedEventArgs e)
    {
        ExtractionController.Instance.LoadLatestCommand.Execute(null);
        UpdateButtonStates();
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (PasswordBox.IsLoaded)
        {
            _vm.Password = PasswordBox.Password;

            if (PasswordBox.Password.Length > 0)
            {
                var db = DatabaseService.Instance;
                var rememberMe = db.GetSetting("remember_me");
                if (rememberMe == "true")
                {
                    db.SaveSetting("password", PasswordBox.Password);
                }
            }
        }
    }

    private void BtnBrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "انتخاب پوشه ذخیره فایلها"
        };
        if (dialog.ShowDialog() == true)
        {
            DatabaseService.Instance.SaveSetting("output_path", dialog.FolderName);
            if (Template.FindName("OutputPathBox", this) is TextBox tb)
                tb.Text = dialog.FolderName;
        }
    }
}
