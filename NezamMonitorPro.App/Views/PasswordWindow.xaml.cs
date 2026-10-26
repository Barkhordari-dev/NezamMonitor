using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace NezamMonitorPro.App.Views;

public partial class PasswordWindow : Window
{
    private bool _isRevealed;

    /// <summary>Creates the login dialog. Pass a message to show an error (e.g. after a wrong password).</summary>
    public PasswordWindow(string? errorMessage = null)
    {
        InitializeComponent();
        Loaded += OnLoaded;

        if (!string.IsNullOrWhiteSpace(errorMessage))
            ErrorText.Text = errorMessage;
    }

    /// <summary>The entered password, regardless of the reveal state.</summary>
    public string Password => _isRevealed ? PasswordTextBox.Text : PasswordBox.Password;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Fade + slide in
        try
        {
            MainCard.Opacity = 0;
            MainCard.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(220))));
            CardTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty,
                new DoubleAnimation(24, 0, new Duration(TimeSpan.FromMilliseconds(260)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
        }
        catch { /* animation is cosmetic only */ }

        if (PasswordBox is not null) PasswordBox.Focus();
        UpdateCapsWarning();
    }

    private void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        var pwd = Password;
        if (string.IsNullOrEmpty(pwd))
        {
            ErrorText.Text = "لطفاً رمز عبور را وارد کنید.";
            FocusPassword();
            return;
        }

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
            LoginButton_Click(sender, e);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CancelButton_Click(sender, e);
        }
    }

    private void RevealToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isRevealed)
        {
            PasswordBox.Password = PasswordTextBox.Text;
            PasswordTextBox.Visibility = Visibility.Collapsed;
            PasswordBox.Visibility = Visibility.Visible;
            _isRevealed = false;
            PasswordBox.Focus();
        }
        else
        {
            PasswordTextBox.Text = PasswordBox.Password;
            PasswordBox.Visibility = Visibility.Collapsed;
            PasswordTextBox.Visibility = Visibility.Visible;
            _isRevealed = true;
            PasswordTextBox.Focus();
            PasswordTextBox.CaretIndex = PasswordTextBox.Text.Length;
        }

        RevealIcon.Foreground = _isRevealed
            ? (System.Windows.Media.Brush)FindResource("PrimaryBrush")
            : (System.Windows.Media.Brush)FindResource("TextSecondaryBrush");
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e) => UpdateCapsWarning();
    private void Window_PreviewKeyUp(object sender, KeyEventArgs e) => UpdateCapsWarning();

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

    private void FocusPassword()
    {
        if (_isRevealed) PasswordTextBox.Focus();
        else PasswordBox.Focus();
    }

    /// <summary>Shows an error message inside the dialog (used for retry flows).</summary>
    public void ShowError(string message) => ErrorText.Text = message;
}
