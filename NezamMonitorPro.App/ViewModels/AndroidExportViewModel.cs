using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using NezamMonitorPro.App.Services;
using NezamMonitorPro.Core.Export;

namespace NezamMonitorPro.App.ViewModels;

/// <summary>
/// ViewModel for Android Export page.
/// </summary>
public sealed class AndroidExportViewModel : ViewModelBase
{
    private readonly NezamMonitorPro.Core.Data.NezamDatabase _db;
    private string _statusMessage = "";
    private string _outputPath = "";
    private string _lastExportInfo = "";

    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    public string OutputPath
    {
        get => _outputPath;
        set
        {
            if (SetProperty(ref _outputPath, value))
                (CopyPathCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

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

    private string _lastExportPath = "";
    public string LastExportPath
    {
        get => _lastExportPath;
        set
        {
            if (SetProperty(ref _lastExportPath, value))
                (OpenOutputFolderCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }
    public string LastExportInfo { get => _lastExportInfo; set => SetProperty(ref _lastExportInfo, value); }

    /// <summary>SEC-015: رمز اختیاری خروجی؛ خالی یعنی بدون رمزنگاری.</summary>
    private string _exportPassword = "";
    public string ExportPassword { get => _exportPassword; set => SetProperty(ref _exportPassword, value); }

    public ICommand BrowseCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand CopyPathCommand { get; }
    public ICommand OpenOutputFolderCommand { get; }

    public AndroidExportViewModel()
    {
        _db = DatabaseService.Instance;
        BrowseCommand = new RelayCommand(Browse);
        ExportCommand = new RelayCommand(DoExport, () => !IsExporting);
        CopyPathCommand = new RelayCommand(CopyOutputPath, () => !string.IsNullOrEmpty(OutputPath));
        OpenOutputFolderCommand = new RelayCommand(OpenOutputFolder, () => !string.IsNullOrEmpty(LastExportPath));
    }

    private void Browse()
    {
        var dialog = new SaveFileDialog
        {
            Title = "ذخیره فایل خروجی اندروید",
            Filter = "فایل داده اندروید|*.nzmdata|فایل فشرده|*.zip",
            DefaultExt = ".nzmdata",
            FileName = $"NezamMonitorPro_Export_{DateTime.Now:yyyyMMdd_HHmmss}.nzmdata"
        };

        if (dialog.ShowDialog() == true)
        {
            OutputPath = dialog.FileName;
            StatusMessage = "مسیر خروجی انتخاب شد. دکمه «ایجاد خروجی» را بزنید.";
        }
    }

    private async void DoExport()
    {
        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            IsStatusError = true;
            StatusMessage = "لطفا ابتدا مسیر ذخیره را انتخاب کنید.";
            return;
        }

        IsExporting = true;
        IsStatusError = false;
        StatusMessage = "در حال ایجاد خروجی...";
        LastExportInfo = "";

        try
        {
            var engine = new AndroidExportEngine(_db);
            var exportPassword = string.IsNullOrWhiteSpace(ExportPassword) ? null : ExportPassword;
            var result = await Task.Run(() => engine.Export(OutputPath, exportPassword: exportPassword));

            if (result.Success)
            {
                var info = $"✅ خروجی با موفقیت ایجاد شد!\n\n"
                         + $"📁 مسیر: {result.OutputPath}\n"
                         + $"📊 تعداد پرونده‌ها: {result.RecordCount}\n"
                         + $"📦 حجم فایل: {result.FileSize / 1024.0:F1} KB\n"
                         + $"🔑 Checksum: {result.Manifest?.Checksum?[..16]}...\n"
                         + $"📅 تاریخ ایجاد: {result.Manifest?.CreatedAt}"
                         + (exportPassword != null ? "\n🔒 خروجی رمزنگاری شده است." : "");

                IsStatusError = false;
                StatusMessage = "✅ ایجاد خروجی موفقیت‌آمیز بود.";
                LastExportPath = result.OutputPath;
                LastExportInfo = info;
            }
            else
            {
                IsStatusError = true;
                StatusMessage = "❌ ایجاد خروجی ناموفق بود.";
                LastExportInfo = string.Join("\n", result.Errors);
            }
        }
        catch (Exception)
        {
            IsStatusError = true;
            StatusMessage = "❌ خطا در ایجاد خروجی.";
            LastExportInfo = "اگر مشکل ادامه داشت، مسیر ذخیره و دسترسی به پوشه را بررسی کن و دوباره تلاش کن.";
        }
        finally
        {
            IsExporting = false;
        }
    }

    private void CopyOutputPath()
    {
        try
        {
            Clipboard.SetText(OutputPath);
            IsStatusError = false;
            StatusMessage = "مسیر کپی شد.";
        }
        catch
        {
            IsStatusError = true;
            StatusMessage = "کپی مسیر انجام نشد. دوباره تلاش کن.";
        }
    }

    private void OpenOutputFolder()
    {
        try
        {
            var folder = LastExportPath;
            if (string.IsNullOrEmpty(folder)) return;
            if (File.Exists(folder)) folder = Path.GetDirectoryName(folder);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                IsStatusError = true;
                StatusMessage = "پوشه خروجی پیدا نشد.";
                return;
            }
            Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
        }
        catch
        {
            IsStatusError = true;
            StatusMessage = "باز کردن پوشه انجام نشد. دوباره تلاش کن.";
        }
    }
}
