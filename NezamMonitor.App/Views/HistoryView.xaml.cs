using System.Windows;
using System.Windows.Controls;
using NezamMonitor.App.Services;
using NezamMonitor.App.ViewModels;

namespace NezamMonitor.App.Views;

public partial class HistoryView : UserControl
{
    private HistoryViewModel _vm;

    public HistoryView()
    {
        InitializeComponent();
        _vm = new HistoryViewModel(DatabaseService.Instance);
        DataContext = _vm;
    }

    private void SetActive_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is long id)
            _vm.SetActive(id);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
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
}
