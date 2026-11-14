using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Win32;
using NezamMonitorPro.Core;
using NezamMonitorPro.Core.Data;
using NezamMonitorPro.Core.Excel;

namespace NezamMonitorPro.App.ViewModels;

public sealed class ExcelExportViewModel : ViewModelBase
{
    private readonly NezamDatabase _db;
    private string _statusMessage = "";
    private string _lastExportPath = "";
    private string _exportFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exports");

    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public string LastExportPath { get => _lastExportPath; set => SetProperty(ref _lastExportPath, value); }

    /// <summary>پوشه‌ای که فایل‌های اکسل در آن ساخته می‌شوند؛ کاربر می‌تواند آن را عوض کند.</summary>
    public string ExportFolder { get => _exportFolder; set => SetProperty(ref _exportFolder, value); }

    private bool _isExporting = false;
    public bool IsExporting
    {
        get => _isExporting;
        set
        {
            if (SetProperty(ref _isExporting, value))
                (ExportCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    /// <summary>خطا بودن پیام وضعیت؛ برای قرمز شدن رنگ پیام در صفحه.</summary>
    private bool _isStatusError = false;
    public bool IsStatusError { get => _isStatusError; set => SetProperty(ref _isStatusError, value); }

    public ObservableCollection<string> ExistingFiles { get; } = new();
    public ICommand ExportCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand BrowseExportFolderCommand { get; }

    public ExcelExportViewModel(NezamDatabase db)
    {
        _db = db;
        ExportCommand = new RelayCommand(DoExport, () => !IsExporting);
        OpenFolderCommand = new RelayCommand(OpenFolder);
        RefreshCommand = new RelayCommand(LoadFiles);
        BrowseExportFolderCommand = new RelayCommand(BrowseExportFolder);
        LoadFiles();
    }

    private void LoadFiles()
    {
        ExistingFiles.Clear();
        if (System.IO.Directory.Exists(ExportFolder))
        {
            foreach (var f in System.IO.Directory.GetFiles(ExportFolder, "*.xlsx"))
            {
                var info = new System.IO.FileInfo(f);
                var date = PersianDateHelper.ToPersianDate(info.CreationTime);
                ExistingFiles.Add($"{System.IO.Path.GetFileName(f)} — {date} {info.CreationTime:HH:mm} — {info.Length / 1024.0:F1} KB");
            }
        }
        StatusMessage = $"{ExistingFiles.Count} فایل اکسل موجود";
    }

    private async void DoExport()
    {
        if (IsExporting) return;

        IsExporting = true;
        IsStatusError = false;
        StatusMessage = "در حال ساخت فایل اکسل...";

        try
        {
            var filePath = await System.Threading.Tasks.Task.Run(() =>
            {
                var cases = _db.LoadCases(_db.GetActiveSnapshotId());
                if (cases.Count == 0) return (string?)null;

                System.IO.Directory.CreateDirectory(ExportFolder);
                var fileName = $"Cases_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                var path = System.IO.Path.Combine(ExportFolder, fileName);
                ExcelExporter.GenerateFullWorkbook(cases, path);
                return (string?)path;
            });

            if (filePath == null)
            {
                IsStatusError = true;
                StatusMessage = "پرونده‌ای برای خروجی وجود ندارد";
                return;
            }

            LastExportPath = filePath;
            // اول لیست به‌روز شود، بعد پیام موفقیت؛ تا پیام موفقیت با «N فایل موجود» بازنویسی نشود.
            LoadFiles();
            StatusMessage = $"فایل ایجاد شد: {System.IO.Path.GetFileName(filePath)}";
        }
        catch
        {
            IsStatusError = true;
            StatusMessage = "ایجاد فایل اکسل انجام نشد. دوباره تلاش کن.";
        }
        finally
        {
            IsExporting = false;
        }
    }

    private void BrowseExportFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "انتخاب پوشه خروجی اکسل",
            FolderName = ExportFolder
        };
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            ExportFolder = dialog.FolderName;
            LoadFiles();
        }
    }

    private void OpenFolder()
    {
        if (System.IO.Directory.Exists(ExportFolder))
            System.Diagnostics.Process.Start("explorer.exe", ExportFolder);
    }
}
