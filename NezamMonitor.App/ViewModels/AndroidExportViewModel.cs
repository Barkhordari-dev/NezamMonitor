using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using NezamMonitor.App.Services;
using NezamMonitor.Core.Export;

namespace NezamMonitor.App.ViewModels;

/// <summary>
/// ViewModel for Android Export page.
/// </summary>
public sealed class AndroidExportViewModel : ViewModelBase
{
    private readonly NezamMonitor.Core.Data.NezamDatabase _db;
    private string _statusMessage = "";
    private string _outputPath = "";
    private bool _isExporting = false;
    private string _lastExportPath = "";
    private string _lastExportInfo = "";

    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public string OutputPath { get => _outputPath; set => SetProperty(ref _outputPath, value); }
    public bool IsExporting { get => _isExporting; set => SetProperty(ref _isExporting, value); }
    public string LastExportPath { get => _lastExportPath; set => SetProperty(ref _lastExportPath, value); }
    public string LastExportInfo { get => _lastExportInfo; set => SetProperty(ref _lastExportInfo, value); }

    public ICommand BrowseCommand { get; }
    public ICommand ExportCommand { get; }

    public AndroidExportViewModel()
    {
        _db = DatabaseService.Instance;
        BrowseCommand = new RelayCommand(Browse);
        ExportCommand = new RelayCommand(DoExport, () => !IsExporting);
    }

    private void Browse()
    {
        var dialog = new SaveFileDialog
        {
            Title = "ذخیره فایل خروجی اندروید",
            Filter = "فایل داده اندروید|*.nzmdata|فایل فشرده|*.zip",
            DefaultExt = ".nzmdata",
            FileName = $"NezamMonitor_Export_{DateTime.Now:yyyyMMdd_HHmmss}.nzmdata"
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
            StatusMessage = "لطفاً ابتدا مسیر ذخیره را انتخاب کنید.";
            return;
        }

        IsExporting = true;
        StatusMessage = "در حال ایجاد خروجی...";
        LastExportInfo = "";

        try
        {
            var engine = new AndroidExportEngine(_db);
            var result = await Task.Run(() => engine.Export(OutputPath));

            if (result.Success)
            {
                var info = $"✅ خروجی با موفقیت ایجاد شد!\n\n"
                         + $"📁 مسیر: {result.OutputPath}\n"
                         + $"📊 تعداد پرونده‌ها: {result.RecordCount}\n"
                         + $"📦 حجم فایل: {result.FileSize / 1024.0:F1} KB\n"
                         + $"🔑 Checksum: {result.Manifest?.Checksum?[..16]}...\n"
                         + $"📅 تاریخ ایجاد: {result.Manifest?.CreatedAt}";

                StatusMessage = "✅ ایجاد خروجی موفقیت‌آمیز بود.";
                LastExportPath = result.OutputPath;
                LastExportInfo = info;
            }
            else
            {
                StatusMessage = "❌ ایجاد خروجی ناموفق بود.";
                LastExportInfo = string.Join("\n", result.Errors);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "❌ خطا در ایجاد خروجی.";
            LastExportInfo = ex.Message;
        }
        finally
        {
            IsExporting = false;
        }
    }
}
