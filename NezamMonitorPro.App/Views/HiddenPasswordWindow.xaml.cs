using System.Windows;
using System.Windows.Input;
using NezamMonitorPro.App.Services;

namespace NezamMonitorPro.App.Views;

/// <summary>
/// Unlock dialog for the hidden menu. On first use it asks for the Drive
/// folder id (once), then for the password. The password is checked live
/// against the first subfolder's name in that Drive folder.
/// </summary>
public partial class HiddenPasswordWindow : Window
{
    public HiddenPasswordWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            // Setup only when no folder id is saved yet; otherwise straight
            // to the password prompt. Each panel focuses its own field.
            // A database error here must not crash the dialog: show setup,
            // and the save step will report the problem.
            string? folderId = null;
            try { folderId = HiddenMenuService.GetFolderId(DatabaseService.Instance); }
            catch { folderId = null; }
            if (folderId == null)
                ShowSetup();
            else
                ShowPassword();
            UpdateCapsWarning();
        };
    }

    private void ShowSetup()
    {
        SetupPanel.Visibility = Visibility.Visible;
        PasswordPanel.Visibility = Visibility.Collapsed;
        ConfirmButton.Visibility = Visibility.Collapsed;
        FolderIdBox.Focus();
    }

    private void ShowPassword(string? info = null)
    {
        SetupPanel.Visibility = Visibility.Collapsed;
        PasswordPanel.Visibility = Visibility.Visible;
        ConfirmButton.Visibility = Visibility.Visible;
        if (!string.IsNullOrWhiteSpace(info))
        {
            InfoText.Text = info;
            InfoText.Visibility = Visibility.Visible;
        }
        else
        {
            InfoText.Text = "";
            InfoText.Visibility = Visibility.Collapsed;
        }
        PasswordBox.Focus();
    }

    private async void SaveFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var raw = FolderIdBox.Text?.Trim() ?? "";
        // رایج‌ترین اشتباه: نوشتن رمز (اسم پوشه) به‌جای شناسه پوشه.
        // اسم پوشه فقط عدد است؛ شناسه واقعی درایو ترکیبی و بلند است.
        if (raw.Length > 0 && raw.All(char.IsDigit))
        {
            SetupErrorText.Text = "این شناسه پوشه نیست؛ اینجا شناسه پوشه درایو را وارد کن، نه رمز را.";
            return;
        }
        var id = HiddenMenuService.ParseFolderId(raw);
        if (id == null)
        {
            SetupErrorText.Text = "شناسه پوشه درست نیست؛ لینک کامل پوشه یا شناسه را وارد کن.";
            return;
        }
        SetupErrorText.Text = "";
        SaveFolderButton.IsEnabled = false;
        SetupCheckingBar.Visibility = Visibility.Visible;
        bool ok;
        try
        {
            ok = await HiddenMenuService.SaveFolderIdAsync(DatabaseService.Instance, id);
        }
        catch
        {
            // async void: هیچ استثنایی نباید از دیالوگ بیرون بزند.
            SetupErrorText.Text = "شناسه ذخیره نشد؛ دوباره تلاش کن.";
            SaveFolderButton.IsEnabled = true;
            SetupCheckingBar.Visibility = Visibility.Collapsed;
            return;
        }
        finally
        {
            SaveFolderButton.IsEnabled = true;
            SetupCheckingBar.Visibility = Visibility.Collapsed;
        }
        if (!ok)
        {
            SetupErrorText.Text = "پوشه پیدا نشد یا اینترنت نیست؛ اینترنت را وصل کن و شناسه را بررسی کن.";
            return;
        }
        // ذخیره تایید شد (دوباره خوانده شد)؛ حالا سراغ رمز می‌رویم.
        ShowPassword("شناسه پوشه ذخیره شد؛ حالا رمز را وارد کن.");
    }

    private async void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        var password = ShowPasswordToggle.IsChecked == true ? VisiblePasswordBox.Text : PasswordBox.Password;
        if (string.IsNullOrWhiteSpace(password))
        {
            ErrorText.Text = "رمز را وارد کن.";
            return;
        }
        ErrorText.Text = "";
        ConfirmButton.IsEnabled = false;
        CheckingBar.Visibility = Visibility.Visible;
        HiddenPasswordResult result;
        try
        {
            result = await HiddenMenuService.CheckPasswordAsync(DatabaseService.Instance, password);
        }
        catch
        {
            // async void: هیچ استثنایی نباید از دیالوگ بیرون بزند.
            // (خود سرویس هم هرگز throw نمی‌کند؛ این فقط سپر دوم است.)
            ErrorText.Text = "خطایی پیش آمد؛ دوباره تلاش کن.";
            ConfirmButton.IsEnabled = true;
            CheckingBar.Visibility = Visibility.Collapsed;
            return;
        }
        finally
        {
            ConfirmButton.IsEnabled = true;
            CheckingBar.Visibility = Visibility.Collapsed;
        }
        switch (result)
        {
            case HiddenPasswordResult.OK:
                HiddenMenuSession.Unlocked = true;
                DialogResult = true;
                Close();
                break;
            case HiddenPasswordResult.WRONG:
                ErrorText.Text = "رمز اشتباه است.";
                break;
            case HiddenPasswordResult.NO_NETWORK:
                // خواندن زنده نشد و اسم کش‌شده‌ای هم نیست: اینترنت یا
                // شناسه پوشه — نه لزوماً «اینترنت نیست».
                ErrorText.Text = "به پوشه درایو وصل نشد؛ اینترنت و شناسه پوشه را بررسی کن و دوباره تلاش کن.";
                break;
            case HiddenPasswordResult.LOCKED:
                // SEC-009: زمان انتظار را نشان می‌دهد.
                var seconds = HiddenMenuService.LockoutRemainingSeconds ?? 0;
                ErrorText.Text = seconds >= 60
                    ? $"چند بار رمز اشتباه زده شد؛ {seconds / 60} دقیقه دیگر دوباره تلاش کن."
                    : $"چند بار رمز اشتباه زده شد؛ {seconds} ثانیه دیگر دوباره تلاش کن.";
                break;
            case HiddenPasswordResult.NO_FOLDER_ID:
                ShowSetup();
                break;
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // A11Y-003: Esc همه‌جا پنجره را می‌بندد.
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
        ShowPasswordLabel.Text = "پنهان";
        VisiblePasswordBox.Focus();
    }

    private void ShowPasswordToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        PasswordBox.Password = VisiblePasswordBox.Text;
        PasswordBox.Visibility = Visibility.Visible;
        VisiblePasswordBox.Visibility = Visibility.Collapsed;
        ShowPasswordLabel.Text = "نمایش";
        PasswordBox.Focus();
    }

    /// <summary>Clicking the «نمایش/پنهان» label toggles the eye, same as the icon.</summary>
    private void ShowPasswordLabel_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        ShowPasswordToggle.IsChecked = ShowPasswordToggle.IsChecked != true;
    }
}
