using System.Windows;
using System.Windows.Input;

namespace NezamMonitorPro.App.Views;

/// <summary>
/// Small prompt for the backup password (M-1): when askConfirm is true
/// (creating a backup) the password is typed twice; when false
/// (restoring) it is typed once. Same look as HiddenPasswordWindow.
/// </summary>
public partial class BackupPasswordWindow : Window
{
    private readonly bool _askConfirm;
    private bool _revealMain;
    private bool _revealConfirm;

    public BackupPasswordWindow(bool askConfirm)
    {
        _askConfirm = askConfirm;
        InitializeComponent();
        ConfirmPanel.Visibility = askConfirm ? Visibility.Visible : Visibility.Collapsed;
        Loaded += (_, _) =>
        {
            PasswordBox.Focus();
            UpdateCapsWarning();
        };
    }

    /// <summary>The entered backup password.</summary>
    public string Password => _revealMain ? VisiblePasswordBox.Text : PasswordBox.Password;

    private string ConfirmPassword => _revealConfirm ? ConfirmVisibleBox.Text : ConfirmPasswordBox.Password;

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        var pwd = Password;
        if (string.IsNullOrWhiteSpace(pwd))
        {
            ErrorText.Text = "رمز پشتیبان را وارد کن.";
            PasswordBox.Focus();
            return;
        }
        if (_askConfirm && !string.Equals(pwd, ConfirmPassword, StringComparison.Ordinal))
        {
            ErrorText.Text = "رمز و تکرارش یکی نیستند؛ دوباره بنویس.";
            ConfirmPasswordBox.Focus();
            return;
        }
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowPasswordToggle_Checked(object sender, RoutedEventArgs e)
    {
        VisiblePasswordBox.Text = PasswordBox.Password;
        VisiblePasswordBox.Visibility = Visibility.Visible;
        PasswordBox.Visibility = Visibility.Collapsed;
        _revealMain = true;
        VisiblePasswordBox.Focus();
    }

    private void ShowPasswordToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        PasswordBox.Password = VisiblePasswordBox.Text;
        PasswordBox.Visibility = Visibility.Visible;
        VisiblePasswordBox.Visibility = Visibility.Collapsed;
        _revealMain = false;
        PasswordBox.Focus();
    }

    private void ShowConfirmToggle_Checked(object sender, RoutedEventArgs e)
    {
        ConfirmVisibleBox.Text = ConfirmPasswordBox.Password;
        ConfirmVisibleBox.Visibility = Visibility.Visible;
        ConfirmPasswordBox.Visibility = Visibility.Collapsed;
        _revealConfirm = true;
        ConfirmVisibleBox.Focus();
    }

    private void ShowConfirmToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        ConfirmPasswordBox.Password = ConfirmVisibleBox.Text;
        ConfirmPasswordBox.Visibility = Visibility.Visible;
        ConfirmVisibleBox.Visibility = Visibility.Collapsed;
        _revealConfirm = false;
        ConfirmPasswordBox.Focus();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // A11Y-003: Esc پنجره را می‌بندد.
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CancelButton_Click(sender, e);
            return;
        }
        UpdateCapsWarning();
    }

    private void Window_PreviewKeyUp(object sender, KeyEventArgs e) => UpdateCapsWarning();

    /// <summary>هشدار Caps Lock، مثل پنجره رمز برنامه.</summary>
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
}
