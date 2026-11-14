using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using NezamMonitorPro.Security.Services;

namespace NezamMonitorPro.App.Views;

public partial class ChangePasswordWindow : Window
{
    private bool _revealCurrent;
    private bool _revealNew;
    private bool _revealConfirm;

    public ChangePasswordWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        // S-029: نشانگر قدرت، رمز جدید را دنبال می‌کند.
        NewPasswordBox.PasswordChanged += (_, _) => UpdateStrength();
        NewPasswordTextBox.TextChanged += (_, _) => UpdateStrength();
    }

    private string CurrentPassword => _revealCurrent ? CurrentPasswordTextBox.Text : CurrentPasswordBox.Password;
    private string NewPassword => _revealNew ? NewPasswordTextBox.Text : NewPasswordBox.Password;
    private string ConfirmPassword => _revealConfirm ? ConfirmPasswordTextBox.Text : ConfirmPasswordBox.Password;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            MainCard.Opacity = 0;
            MainCard.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(220))));
            CardTranslate.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(24, 0, new Duration(TimeSpan.FromMilliseconds(260)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
        }
        catch { }

        CurrentPasswordBox.Focus();
        UpdateCapsWarning();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var current = CurrentPassword;
        var next = NewPassword;
        var confirm = ConfirmPassword;

        if (string.IsNullOrEmpty(current))
        {
            ErrorText.Text = "لطفا رمز عبور فعلی را وارد کنید.";
            CurrentPasswordBox.Focus();
            return;
        }

        if (string.IsNullOrEmpty(next))
        {
            ErrorText.Text = "لطفا رمز عبور جدید را وارد کنید.";
            NewPasswordBox.Focus();
            return;
        }

        // هیچ قانون طول یا ترکیبی برای رمز جدید اعمال نمی‌شود — فقط
        // خالی نبودن، متفاوت بودن از رمز فعلی و یکسان بودن با تکرارش
        // بررسی می‌شود. نشانگر قدرت و متن راهنما فقط پیشنهاد می‌دهند.
        if (current == next)
        {
            ErrorText.Text = "رمز عبور جدید باید با رمز عبور فعلی متفاوت باشد.";
            NewPasswordBox.Focus();
            return;
        }

        if (!string.Equals(next, confirm, StringComparison.Ordinal))
        {
            ErrorText.Text = "رمز عبور جدید و تکرار آن یکسان نیستند.";
            ConfirmPasswordBox.Focus();
            return;
        }

        try
        {
            var appDir = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName) ?? AppDomain.CurrentDomain.BaseDirectory;
            var credPath = Path.Combine(appDir, "data", "credentials.json");
            var dbPath = Path.Combine(appDir, "data", "nezam_monitor.db");

            var credentialStore = new CredentialStore(credPath);
            var encryptionService = new DatabaseEncryptionService(credentialStore, dbPath);

            encryptionService.ChangePassword(current, next);

            DialogResult = true;
            Close();
        }
        catch (UnauthorizedAccessException)
        {
            ErrorText.Text = "رمز عبور فعلی اشتباه است.";
            CurrentPasswordBox.Focus();
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"خطا در تغییر رمز عبور: {ex.Message}";
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch { }
        }
    }

    private void Password_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            SaveButton_Click(sender, e);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CancelButton_Click(sender, e);
        }
    }

    private void CurrentRevealToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_revealCurrent)
        {
            CurrentPasswordBox.Password = CurrentPasswordTextBox.Text;
            CurrentPasswordTextBox.Visibility = Visibility.Collapsed;
            CurrentPasswordBox.Visibility = Visibility.Visible;
            _revealCurrent = false;
            CurrentPasswordBox.Focus();
        }
        else
        {
            CurrentPasswordTextBox.Text = CurrentPasswordBox.Password;
            CurrentPasswordBox.Visibility = Visibility.Collapsed;
            CurrentPasswordTextBox.Visibility = Visibility.Visible;
            _revealCurrent = true;
            CurrentPasswordTextBox.Focus();
            CurrentPasswordTextBox.CaretIndex = CurrentPasswordTextBox.Text.Length;
        }
        SetIconColor(CurrentRevealIcon, _revealCurrent);
    }

    private void NewRevealToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_revealNew)
        {
            NewPasswordBox.Password = NewPasswordTextBox.Text;
            NewPasswordTextBox.Visibility = Visibility.Collapsed;
            NewPasswordBox.Visibility = Visibility.Visible;
            _revealNew = false;
            NewPasswordBox.Focus();
        }
        else
        {
            NewPasswordTextBox.Text = NewPasswordBox.Password;
            NewPasswordBox.Visibility = Visibility.Collapsed;
            NewPasswordTextBox.Visibility = Visibility.Visible;
            _revealNew = true;
            NewPasswordTextBox.Focus();
            NewPasswordTextBox.CaretIndex = NewPasswordTextBox.Text.Length;
        }
        SetIconColor(NewRevealIcon, _revealNew);
    }

    private void ConfirmRevealToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_revealConfirm)
        {
            ConfirmPasswordBox.Password = ConfirmPasswordTextBox.Text;
            ConfirmPasswordTextBox.Visibility = Visibility.Collapsed;
            ConfirmPasswordBox.Visibility = Visibility.Visible;
            _revealConfirm = false;
            ConfirmPasswordBox.Focus();
        }
        else
        {
            ConfirmPasswordTextBox.Text = ConfirmPasswordBox.Password;
            ConfirmPasswordBox.Visibility = Visibility.Collapsed;
            ConfirmPasswordTextBox.Visibility = Visibility.Visible;
            _revealConfirm = true;
            ConfirmPasswordTextBox.Focus();
            ConfirmPasswordTextBox.CaretIndex = ConfirmPasswordTextBox.Text.Length;
        }
        SetIconColor(ConfirmRevealIcon, _revealConfirm);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // A11Y-003: Esc همه‌جا پنجره را می‌بندد (نه فقط وقتی فوکوس روی کادر رمز است).
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CancelButton_Click(sender, e);
            return;
        }
        UpdateCapsWarning();
    }

    private void Window_PreviewKeyUp(object sender, KeyEventArgs e) => UpdateCapsWarning();

    /// <summary>S-027: هشدار Caps Lock، مثل پنجره رمز برنامه.</summary>
    private void UpdateCapsWarning()
    {
        try
        {
            CapsWarning.Visibility = Keyboard.IsKeyToggled(Key.CapsLock)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        catch { }
    }

    /// <summary>S-029: نشانگر قدرت رمز جدید — همان منطق پنجره ساخت رمز.</summary>
    private void UpdateStrength()
    {
        var pwd = NewPassword;
        int score = 0;
        if (pwd.Length >= 8) score++;
        if (pwd.Length >= 12) score++;
        if (pwd.Any(char.IsDigit)) score++;
        if (pwd.Any(c => !char.IsLetterOrDigit(c))) score++;

        var bars = new[] { StrengthBar1, StrengthBar2, StrengthBar3, StrengthBar4 };
        string[] labels = { "خیلی ضعیف", "ضعیف", "متوسط", "خوب", "قوی" };

        Brush on = Res("ErrorBrush");
        if (score >= 4) on = Res("SuccessBrush");
        else if (score == 3) on = Res("PrimaryBrush");
        else if (score == 2) on = Res("WarningBrush");

        for (int i = 0; i < bars.Length; i++)
            bars[i].Background = i < score ? on : Res("BorderBrush");

        StrengthText.Text = pwd.Length == 0 ? "" : labels[Math.Clamp(score, 0, 4)];
        StrengthText.Foreground = on;
    }

    private static Brush Res(string key)
    {
        try
        {
            if (Application.Current?.TryFindResource(key) is Brush b) return b;
        }
        catch { }
        return Brushes.Gray;
    }

    private static void SetIconColor(TextBlock icon, bool revealed)
    {
        try
        {
            icon.Foreground = revealed
                ? (Brush)Application.Current.Resources["PrimaryBrush"]
                : (Brush)Application.Current.Resources["TextSecondaryBrush"];
        }
        catch { }
    }
}
