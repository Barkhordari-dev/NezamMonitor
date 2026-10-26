using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NezamMonitorPro.App.Services;
using NezamMonitorPro.App.ViewModels;

namespace NezamMonitorPro.App.Views;

public partial class HistoryView : UserControl, INavigationAware
{
    private readonly HistoryViewModel? _vm;

    /// <summary>هنگام بازگشت به این صفحه، اسنپ‌شات‌ها و بایگانی دوباره خوانده می‌شوند.</summary>
    public void OnNavigatedTo()
    {
        try { _vm?.RefreshAll(); }
        catch (Exception ex) { Log("HistoryView.OnNavigatedTo", ex); }
    }

    public HistoryView()
    {
        InitializeComponent();

        try
        {
            _vm = new HistoryViewModel(DatabaseService.Instance);
            DataContext = _vm;

            var snapshots = _vm.Snapshots.Count;
            var legacy = _vm.LegacyDatabases.Count;
            var detected = _vm.DetectedDatabases.Count;
            LogInfo($"HistoryView ctor OK: snapshots={snapshots}, legacy={legacy}, detected={detected}");

            Loaded += (_, _) => LogInfo(
                $"HistoryView loaded: w={ActualWidth:0} h={ActualHeight:0} " +
                $"shell={HistoryRoot?.ActualWidth:0}x{HistoryRoot?.ActualHeight:0} " +
                $"dc={(DataContext is null ? "null" : DataContext.GetType().Name)}");
        }
        catch (Exception ex)
        {
            Log("HistoryView ctor", ex);
            _vm = null;
        }
    }

    private static void LogInfo(string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName)
                      ?? AppDomain.CurrentDomain.BaseDirectory;
            File.AppendAllText(Path.Combine(dir, "startup.log"), $"[{DateTime.Now}] {message}\n");
        }
        catch { }
    }

    private void SetActive_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        if (sender is Button btn && btn.Tag is long id)
            _vm.SetActive(id);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        if (sender is Button btn && btn.Tag is long id)
        {
            var result = MessageBox.Show(
                "آیا از حذف اسنپ‌شات #" + id + " مطمئن هستید؟\n\nاین عمل غیرقابل بازگشت است.",
                "تأیید حذف",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
                _vm.DeleteSnapshot(id);
        }
    }

    private void Merge_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        var selectedCount = _vm.Snapshots.Count(s => s.IsSelectedForMerge);
        if (selectedCount < 2)
        {
            MessageBox.Show("حداقل ۲ اسنپ‌شات انتخاب کنید.", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"آیا از ادغام {selectedCount} اسنپ‌شات مطمئن هستید؟\n\nاسنپ‌شات جدید ایجاد می‌شود.",
            "تأیید ادغام",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
            _vm.MergeSnapshots();
    }

    // ═════════════ بایگانی پایگاه‌داده‌های نسخه‌های قبلی ═════════════

    private void AddLegacy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "انتخاب پایگاه‌دادهٔ نسخهٔ قبلی",
                Filter = "پایگاه‌داده SQLite (*.db;*.sqlite;*.db3;*.sqlite3)|*.db;*.sqlite;*.db3;*.sqlite3|همهٔ فایل‌ها (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog() != true) return;
            if (_vm is null) return;

            var status = _vm.AttachLegacy(dialog.FileName);

            MessageBox.Show(
                status + "\n\n" +
                "نکته: یک کپی در پوشهٔ data\\legacy ذخیره شد و فقط‌خواندنی است. " +
                "پایگاه‌دادهٔ رمزنگاری‌شدهٔ فعلی برنامه تغییری نکرده است.",
                "بایگانی پایگاه‌داده",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log("HistoryView.AddLegacy", ex);
            MessageBox.Show("افزودن فایل ناموفق بود:\n" + ex.Message, "خطا",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void AttachDetected_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_vm is null) return;
            if (sender is not Button btn || btn.Tag is not string path) return;

            var status = _vm.AttachDetected(path);
            MessageBox.Show(status, "بایگانی پایگاه‌داده", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log("HistoryView.AttachDetected", ex);
            MessageBox.Show("افزودن به بایگانی ناموفق بود:\n" + ex.Message, "خطا",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void ImportLegacy_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        if (sender is not Button btn || btn.Tag is not string path) return;

        var confirm = MessageBox.Show(
            "داده‌های این پایگاه‌دادهٔ قدیمی به برنامه ایمپورت شود؟\n\n" +
            "• یک اسنپ‌شات جدید ساخته می‌شود و داده‌های فعلی حذف نمی‌شوند.\n" +
            "• فایل قدیمی تغییر نمی‌کند و پایگاه‌دادهٔ رمزنگاری‌شدهٔ فعلی هم دست‌نخورده می‌ماند.",
            "ایمپورت پایگاه‌دادهٔ نسخهٔ قبلی",
            MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            Cursor = System.Windows.Input.Cursors.Wait;
            var message = await _vm.ImportLegacyAsync(path);
            var failed = message.Contains("ناموفق");
            MessageBox.Show(message, "نتیجهٔ ایمپورت", MessageBoxButton.OK,
                failed ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log("HistoryView.ImportLegacy", ex);
            MessageBox.Show("ایمپورت ناموفق بود: " + ex.Message, "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Cursor = System.Windows.Input.Cursors.Arrow;
        }
    }

    private void RemoveLegacy_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        if (sender is not Button btn || btn.Tag is not string path) return;

        var result = MessageBox.Show(
            "این مورد از بایگانی حذف شود؟\n\nفقط کپی بایگانی‌شده حذف می‌شود و فایل اصلی شما دست‌نخورده باقی می‌ماند.",
            "حذف از بایگانی",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        var item = _vm.LegacyDatabases.FirstOrDefault(x => x.Path == path);
        if (item is not null) _vm.RemoveLegacy(item);
    }

    private void ScanLegacy_Click(object sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        try
        {
            _vm.ScanCandidates();
        }
        catch (Exception ex)
        {
            Log("HistoryView.ScanLegacy", ex);
        }
    }

    private static void Log(string where, Exception ex)
    {
        try
        {
            var dir = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName)
                      ?? AppDomain.CurrentDomain.BaseDirectory;
            File.AppendAllText(Path.Combine(dir, "startup.log"),
                $"[{DateTime.Now}] {where}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n");
        }
        catch { }
    }
}
