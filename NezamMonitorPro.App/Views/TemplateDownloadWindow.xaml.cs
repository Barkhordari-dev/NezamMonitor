using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NezamMonitorPro.App.Services;

namespace NezamMonitorPro.App.Views;

/// <summary>
/// Browses the public Google Drive templates folder (same folder as the
/// Android app) and downloads the chosen .docx files into the app's
/// templates folder. No sign-in, no API key — the folder is public.
/// </summary>
public partial class TemplateDownloadWindow : Window
{
    private sealed class EntryItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        public required DriveListingService.DriveEntry Entry { get; init; }
        public string Name => Entry.Name;
        public bool IsFile => !Entry.IsFolder;
        public string TypeDisplay => Entry.IsFolder ? "📁 پوشه" : "📄 Word";
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private readonly string _templatesDir;
    private readonly ObservableCollection<EntryItem> _items = new();
    private readonly Stack<(string Id, string Name)> _history = new();
    private string _currentFolderId = DriveListingService.TemplatesFolderId;
    private string _currentFolderName = "پوشه تمپلیت‌ها";
    private bool _busy;

    public TemplateDownloadWindow()
    {
        InitializeComponent();
        _templatesDir = Path.Combine(DatabaseService.GetAppDirectory(), "templates");
        EntriesGrid.ItemsSource = _items;
        Loaded += async (_, _) => await LoadFolderAsync(_currentFolderId, _currentFolderName);
    }

    private async Task LoadFolderAsync(string folderId, string folderName)
    {
        if (_busy) return;
        _busy = true;
        LoadingBar.Visibility = Visibility.Visible;
        StatusText.Text = "در حال خواندن پوشه…";
        DownloadButton.IsEnabled = false;
        try
        {
            var entries = await DriveListingService.FetchListingAsync(folderId);
            _currentFolderId = folderId;
            _currentFolderName = folderName;
            FolderNameText.Text = folderName;
            BackButton.Visibility = _history.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            _items.Clear();
            foreach (var e in entries)
            {
                if (e.IsFolder || e.Name.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
                    _items.Add(new EntryItem { Entry = e });
            }
            StatusText.Text = _items.Count == 0
                ? "پوشه خالی است یا خوانده نشد — اینترنت را بررسی کن یا پوشه را در مرورگر باز کن."
                : $"{_items.Count} مورد پیدا شد؛ فایل‌های Word را تیک بزن و دانلود کن.";
        }
        finally
        {
            LoadingBar.Visibility = Visibility.Collapsed;
            DownloadButton.IsEnabled = true;
            _busy = false;
        }
    }

    private EntryItem? SelectedItem => EntriesGrid.SelectedItem as EntryItem;

    private async void OpenSelectedFolder()
    {
        var item = SelectedItem;
        if (item == null || !item.Entry.IsFolder) return;
        _history.Push((_currentFolderId, _currentFolderName));
        await LoadFolderAsync(item.Entry.Id, item.Entry.Name);
    }

    /// <summary>G-022: دابل‌کلیک روی پوشه واردش می‌شود؛ روی فایل تیک آن را می‌زند.</summary>
    private void EntriesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var item = SelectedItem;
        if (item == null) return;
        if (item.Entry.IsFolder) OpenSelectedFolder();
        else item.IsSelected = !item.IsSelected;
    }

    /// <summary>دکمه «باز کردن» فقط روی پوشه فعال است (G-022).</summary>
    private void EntriesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        OpenFolderButton.IsEnabled = SelectedItem?.Entry.IsFolder == true;
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem?.Entry.IsFolder == true) OpenSelectedFolder();
        else StatusText.Text = "روی فایل کاری نمی‌کند؛ برای دانلود، فایل را تیک بزن.";
    }

    private async void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_history.Count == 0) return;
        var (id, name) = _history.Pop();
        await LoadFolderAsync(id, name);
    }

    private void OpenBrowserButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = $"https://drive.google.com/drive/folders/{_currentFolderId}",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusText.Text = "مرورگر باز نشد: " + ex.Message;
        }
    }

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var selected = _items.Where(i => i.IsFile && i.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusText.Text = "اول فایل‌هایی را که می‌خواهی دانلود شود تیک بزن.";
            return;
        }

        // Overwrite warning: one dialog for all existing files.
        var existing = selected.Where(i => File.Exists(Path.Combine(_templatesDir, i.Name))).ToList();
        var toDownload = selected;
        if (existing.Count > 0)
        {
            var names = string.Join("\n", existing.Select(i => "• " + i.Name));
            var answer = MessageBox.Show(
                "این فایل‌ها از قبل در پوشه تمپلیت‌ها هست و بازنویسی می‌شوند:\n" + names +
                "\n\n«بله» = بازنویسی همه، «خیر» = فقط فایل‌های تازه دانلود می‌شوند، «انصراف» = لغو دانلود.",
                "بازنویسی فایل‌های موجود؟",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);
            if (answer == MessageBoxResult.Cancel) return;
            if (answer == MessageBoxResult.No)
                toDownload = selected.Except(existing).ToList();
            if (toDownload.Count == 0)
            {
                StatusText.Text = "همه فایل‌های انتخاب‌شده از قبل بودند و رد شدند.";
                return;
            }
        }

        _busy = true;
        LoadingBar.Visibility = Visibility.Visible;
        DownloadButton.IsEnabled = false;
        StatusText.Text = $"در حال دانلود {toDownload.Count} فایل…";
        try
        {
            var ok = 0;
            var failed = new List<string>();
            foreach (var item in toDownload)
            {
                if (await DriveListingService.DownloadFileAsync(item.Entry.Id, item.Name, _templatesDir))
                    ok++;
                else
                    failed.Add(item.Name);
            }
            StatusText.Text = failed.Count == 0
                ? $"✅ {ok} فایل در {_templatesDir} قرار گرفت"
                : $"✅ {ok} فایل قرار گرفت؛ این‌ها دانلود نشد: {string.Join("، ", failed)}";
            foreach (var item in toDownload.Where(i => !failed.Contains(i.Name)))
                item.IsSelected = false;
        }
        finally
        {
            LoadingBar.Visibility = Visibility.Collapsed;
            DownloadButton.IsEnabled = true;
            _busy = false;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
