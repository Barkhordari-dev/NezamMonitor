using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
using NezamMonitorPro.App.Services;
using NezamMonitorPro.App.ViewModels;
using NezamMonitorPro.Core.Data;

namespace NezamMonitorPro.App.Views;

public partial class UpdateView : UserControl, INavigationAware
{
    private readonly UpdateViewModel _vm;
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

        // ARCH-001: ViewModel از کانتینر DI گرفته می‌شود.
        _vm = App.Services.GetRequiredService<UpdateViewModel>();
        DataContext = _vm;

        Loaded += (_, _) =>
        {
            SubscribeController();
            UpdateButtonStates();
            UpdateCapsWarning();
            // Show the saved password as dots in the password box (it was
            // loaded into the ViewModel but PasswordBox.Password is not
            // bindable, so it stayed empty until now). The eye button then
            // reveals exactly this saved password.
            if (PasswordBox != null && string.IsNullOrEmpty(PasswordBox.Password)
                && !string.IsNullOrEmpty(_vm.Password))
                PasswordBox.Password = _vm.Password;
            if (!_initialized)
            {
                _initialized = true;
                try { ApplyStageVisual(ExtractionController.Instance.Stage, animate: false); }
                catch { }
            }
        };

        // مهم: اشتراک در رویداد باید هنگام خروج لغو شود، وگرنه نسخه‌های قدیمی زنده می‌مانند
        // و روی کنترل‌های آزادشده کار می‌کنند (منبع کرش).
        Unloaded += (_, _) =>
        {
            UnsubscribeController();
            // DUP-005: اطلاعات ورود موقع بستن صفحه ذخیره می‌شوند (نه با هر کلید).
            try { _vm.SaveCredentials(); } catch { }
        };

    }

    /// <summary>هنگام بازگشت به این صفحه (ویو کش شده) وضعیت دکمه‌ها و تصویر مرحله تازه می‌شود.</summary>
    public void OnNavigatedTo()
    {
        try
        {
            SubscribeController();
            UpdateButtonStates();
            ApplyStageVisual(ExtractionController.Instance.Stage, animate: false);
        }
        catch { }
    }


    private bool _subscribed;

    private void SubscribeController()
    {
        if (_subscribed) return;
        try
        {
            ExtractionController.Instance.PropertyChanged += UpdateView_PropertyChanged;
            _subscribed = true;
        }
        catch { }
    }

    private void UnsubscribeController()
    {
        if (!_subscribed) return;
        try
        {
            ExtractionController.Instance.PropertyChanged -= UpdateView_PropertyChanged;
        }
        catch { }
        _subscribed = false;
    }


    private void UpdateView_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // کنترلر ممکن است رویداد را از یک ترد پس‌زمینه بالا ببرد؛
        // دست‌زدن به کنترل‌های UI از ترد دیگر = کرش قطعی برنامه.
        if (!Dispatcher.CheckAccess())
        {
            try { Dispatcher.BeginInvoke(new Action(() => UpdateView_PropertyChanged(sender, e))); }
            catch { }
            return;
        }

        try
        {
            UpdateView_PropertyChangedCore(e);
        }
        catch
        {
            // هرگز نگذار خطای یک ویو به کنترلر/برنامه سرایت کند
        }
    }

    private void UpdateView_PropertyChangedCore(System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ExtractionController.IsBusy))
        {
            UpdateButtonStates();
        }
        else if (e.PropertyName == nameof(ExtractionController.Progress))
        {
            // S-005: پیشرفت مستقیم از رویداد کنترلر می‌آید؛ تایمر polling حذف شد.
            if (ProgressFill != null)
            {
                ProgressFill.Tag = Math.Max(0, Math.Min(100, ExtractionController.Instance.Progress));
                UpdateProgressWidth();
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
        // S-004: نام کاربری و رمز خالی را قبل از شروع می‌گیریم.
        if (string.IsNullOrWhiteSpace(_vm.Username) || string.IsNullOrWhiteSpace(_vm.Password))
        {
            MessageBox.Show("نام کاربری و رمز سامانه را وارد کن.", "اطلاعات ناقص",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            UsernameBox.Focus();
            return;
        }
        // S-008: «شروع از پرونده شماره» حداقل ۱ و «تعداد» حداقل ۰ است (۰ یعنی همه).
        if (!int.TryParse(StartFromBox.Text, out var startFrom) || startFrom < 1)
        {
            MessageBox.Show("«شروع از پرونده شماره» باید ۱ یا بیشتر باشد؛ ۱ یعنی از اولین پرونده.", "عدد اشتباه",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            StartFromBox.Focus();
            StartFromBox.SelectAll();
            return;
        }
        if (!int.TryParse(MaxCasesBox.Text, out var maxCases) || maxCases < 0)
        {
            MessageBox.Show("«تعداد» باید ۰ یا بیشتر باشد؛ ۰ یعنی همه پرونده‌ها.", "عدد اشتباه",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            MaxCasesBox.Focus();
            MaxCasesBox.SelectAll();
            return;
        }

        var ec = ExtractionController.Instance;
        ec.Username = _vm.Username;
        ec.Password = _vm.Password;
        ec.RememberMe = _vm.RememberMe;
        // DUP-005: اطلاعات ورود فقط موقع شروع ذخیره می‌شوند (نه با هر کلید).
        _vm.SaveCredentials();

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

    /// <summary>نمایش/پنهان‌سازی رمز عبور واردشده (آیکون چشم).</summary>
    private void TogglePassword_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ShowPasswordToggle.IsChecked == true)
            {
                VisiblePasswordBox.Text = PasswordBox.Password;
                PasswordBox.Visibility = Visibility.Collapsed;
                VisiblePasswordBox.Visibility = Visibility.Visible;
            }
            else
            {
                PasswordBox.Password = VisiblePasswordBox.Text;
                PasswordBox.Visibility = Visibility.Visible;
                VisiblePasswordBox.Visibility = Visibility.Collapsed;
            }
        }
        catch { }
    }

    /// <summary>Editing the revealed password keeps the ViewModel in sync.</summary>
    private void VisiblePasswordBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (VisiblePasswordBox.IsLoaded && VisiblePasswordBox.Visibility == Visibility.Visible)
            _vm.Password = VisiblePasswordBox.Text;
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (PasswordBox.IsLoaded)
        {
            _vm.Password = PasswordBox.Password;
            if (VisiblePasswordBox != null && VisiblePasswordBox.Visibility == Visibility.Visible)
                VisiblePasswordBox.Text = PasswordBox.Password;
            // نام کاربری و رمز موقع شروع به‌روزرسانی یا بستن صفحه ذخیره می‌شوند، نه با هر کلید.
        }
    }

    /// <summary>S-027: هشدار Caps Lock، مثل پنجره رمز برنامه.</summary>
    private void UpdateView_PreviewKeyDown(object sender, KeyEventArgs e) => UpdateCapsWarning();

    /// <summary>S-027: هشدار Caps Lock، مثل پنجره رمز برنامه.</summary>
    private void UpdateView_PreviewKeyUp(object sender, KeyEventArgs e) => UpdateCapsWarning();

    private void UpdateCapsWarning()
    {
        if (CapsWarning != null)
            CapsWarning.Visibility = Keyboard.IsKeyToggled(Key.CapsLock)
                ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnBrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "انتخاب پوشه ذخیره فایل‌ها"
        };
        if (dialog.ShowDialog() == true)
        {
            // S-002: مسیر همان لحظه هم در کادر و هم در کنترلر اعمال می‌شود.
            DatabaseService.Instance.SaveSetting(SettingKeys.OutputPath, dialog.FolderName);
            OutputPathBox.Text = dialog.FolderName;
            ExtractionController.Instance.OutputPath = dialog.FolderName;
        }
    }
}
