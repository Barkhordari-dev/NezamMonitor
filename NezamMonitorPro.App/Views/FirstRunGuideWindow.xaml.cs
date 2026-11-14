using System.Windows;

namespace NezamMonitorPro.App.Views;

/// <summary>
/// M-6: راهنمای شروع اول — فقط بار اول بعد از لود پنجره اصلی نشان داده
/// می‌شود (تنظیم first_run_done در دیتابیس). هر دو دکمه پنجره را می‌بندند
/// و نمایش دوباره را خاموش می‌کنند؛ «فهمیدم» یعنی «خواندم و فهمیدم» و
/// «دیگر نشان نده» یعنی «حتی اگر نفهمیدم هم دیگر نشان نده».
/// </summary>
public partial class FirstRunGuideWindow : Window
{
    /// <summary>True یعنی دیگر نشان نده (MainWindow تنظیم first_run_done را ست می‌کند).</summary>
    public bool DoNotShowAgain { get; private set; }

    public FirstRunGuideWindow()
    {
        InitializeComponent();
    }

    private void Understood_Click(object sender, RoutedEventArgs e)
    {
        DoNotShowAgain = true;
        DialogResult = true;
        Close();
    }

    private void DoNotShow_Click(object sender, RoutedEventArgs e)
    {
        DoNotShowAgain = true;
        DialogResult = true;
        Close();
    }
}
