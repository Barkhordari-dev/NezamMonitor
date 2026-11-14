using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace NezamMonitorPro.App.Views;

public partial class AndroidExportView : UserControl
{
    public AndroidExportView()
    {
        InitializeComponent();
        // ARCH-001: ViewModel از کانتینر DI گرفته می‌شود.
        DataContext = App.Services.GetRequiredService<ViewModels.AndroidExportViewModel>();
    }

    /// <summary>نمایش/پنهان‌سازی رمز خروجی (آیکون چشم) — همان الگوی UpdateView.</summary>
    private void ToggleExportPassword_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ShowExportPasswordToggle.IsChecked == true)
            {
                ExportVisiblePasswordBox.Text = ExportPasswordBox.Password;
                ExportPasswordBox.Visibility = Visibility.Collapsed;
                ExportVisiblePasswordBox.Visibility = Visibility.Visible;
            }
            else
            {
                ExportPasswordBox.Password = ExportVisiblePasswordBox.Text;
                ExportPasswordBox.Visibility = Visibility.Visible;
                ExportVisiblePasswordBox.Visibility = Visibility.Collapsed;
            }
        }
        catch { }
    }

    /// <summary>Editing the revealed password keeps the ViewModel in sync.</summary>
    private void ExportVisiblePasswordBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ExportVisiblePasswordBox.IsLoaded && ExportVisiblePasswordBox.Visibility == Visibility.Visible)
            SetExportPassword(ExportVisiblePasswordBox.Text);
    }

    private void ExportPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (ExportPasswordBox.IsLoaded)
        {
            SetExportPassword(ExportPasswordBox.Password);
            if (ExportVisiblePasswordBox.Visibility == Visibility.Visible)
                ExportVisiblePasswordBox.Text = ExportPasswordBox.Password;
        }
    }

    private void SetExportPassword(string password)
    {
        if (DataContext is ViewModels.AndroidExportViewModel vm)
            vm.ExportPassword = password;
        // هشدار «رمزنگاری نشده» فقط وقتی رمز خالی است دیده می‌شود.
        UnencryptedWarning.Visibility = string.IsNullOrEmpty(password) ? Visibility.Visible : Visibility.Collapsed;
    }
}
