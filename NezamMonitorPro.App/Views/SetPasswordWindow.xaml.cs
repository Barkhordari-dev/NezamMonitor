using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NezamMonitorPro.App.Views;

public partial class SetPasswordWindow : Window
{
    private bool _revealMain;
    private bool _revealConfirm;

    public SetPasswordWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        PasswordBox.PasswordChanged += (_, _) => UpdateStrength();
        PasswordTextBox.TextChanged += (_, _) => UpdateStrength();
    }

    /// <summary>The new database password.</summary>
    public string Password => _revealMain ? PasswordTextBox.Text : PasswordBox.Password;

    private string ConfirmPassword => _revealConfirm ? ConfirmTextBox.Text : ConfirmPasswordBox.Password;

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

        PasswordBox.Focus();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var pwd = Password;
        var confirm = ConfirmPassword;

        if (string.IsNullOrEmpty(pwd))
        {
            ErrorText.Text = "لطفاً رمز عبور را وارد کنید.";
            PasswordBox.Focus();
            return;
        }

        if (pwd.Length < 8)
        {
            ErrorText.Text = "رمز عبور باید حداقل ۸ کاراکتر باشد.";
            PasswordBox.Focus();
            return;
        }

        if (!string.Equals(pwd, confirm, StringComparison.Ordinal))
        {
            ErrorText.Text = "رمز عبور و تکرار آن یکسان نیستند.";
            ConfirmPasswordBox.Focus();
            return;
        }

        ErrorText.Text = "";
        DialogResult = true;
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

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (_revealMain) ConfirmTextBox.Focus();
            else ConfirmPasswordBox.Focus();
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CancelButton_Click(sender, e);
        }
    }

    private void ConfirmPasswordBox_KeyDown(object sender, KeyEventArgs e)
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

    private void RevealToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_revealMain)
        {
            PasswordBox.Password = PasswordTextBox.Text;
            PasswordTextBox.Visibility = Visibility.Collapsed;
            PasswordBox.Visibility = Visibility.Visible;
            _revealMain = false;
            PasswordBox.Focus();
        }
        else
        {
            PasswordTextBox.Text = PasswordBox.Password;
            PasswordBox.Visibility = Visibility.Collapsed;
            PasswordTextBox.Visibility = Visibility.Visible;
            _revealMain = true;
            PasswordTextBox.Focus();
            PasswordTextBox.CaretIndex = PasswordTextBox.Text.Length;
        }
        SetIconColor(RevealIcon, _revealMain);
    }

    private void ConfirmRevealToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_revealConfirm)
        {
            ConfirmPasswordBox.Password = ConfirmTextBox.Text;
            ConfirmTextBox.Visibility = Visibility.Collapsed;
            ConfirmPasswordBox.Visibility = Visibility.Visible;
            _revealConfirm = false;
            ConfirmPasswordBox.Focus();
        }
        else
        {
            ConfirmTextBox.Text = ConfirmPasswordBox.Password;
            ConfirmPasswordBox.Visibility = Visibility.Collapsed;
            ConfirmTextBox.Visibility = Visibility.Visible;
            _revealConfirm = true;
            ConfirmTextBox.Focus();
            ConfirmTextBox.CaretIndex = ConfirmTextBox.Text.Length;
        }
        SetIconColor(ConfirmRevealIcon, _revealConfirm);
    }

    private void SetIconColor(TextBlock icon, bool active)
    {
        try
        {
            icon.Foreground = active
                ? (Brush)FindResource("PrimaryBrush")
                : (Brush)FindResource("TextSecondaryBrush");
        }
        catch { }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e) { }

    private void UpdateStrength()
    {
        var pwd = Password;
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
}
