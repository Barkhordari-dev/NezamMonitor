using System.Collections.ObjectModel;
using System.Windows.Input;
using NezamMonitor.Core.Data;
using NezamMonitor.Core.Excel;

namespace NezamMonitor.App.ViewModels;

public sealed class ExcelExportViewModel : ViewModelBase
{
    private readonly NezamDatabase _db;
    private string _statusMessage = "";
    private string _lastExportPath = "";

    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public string LastExportPath { get => _lastExportPath; set => SetProperty(ref _lastExportPath, value); }
    public ObservableCollection<string> ExistingFiles { get; } = new();
    public ICommand ExportCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand RefreshCommand { get; }

    public ExcelExportViewModel(NezamDatabase db)
    {
        _db = db;
        ExportCommand = new RelayCommand(Export);
        OpenFolderCommand = new RelayCommand(OpenFolder);
        RefreshCommand = new RelayCommand(LoadFiles);
        LoadFiles();
    }

    private void LoadFiles()
    {
        ExistingFiles.Clear();
        var exeDir = AppDomain.CurrentDomain.BaseDirectory;
        var exportDir = System.IO.Path.Combine(exeDir, "exports");
        if (System.IO.Directory.Exists(exportDir))
        {
            foreach (var f in System.IO.Directory.GetFiles(exportDir, "*.xlsx"))
                ExistingFiles.Add(System.IO.Path.GetFileName(f));
        }
        StatusMessage = $"{ExistingFiles.Count} فایل اکسل موجود";
    }

    private void Export()
    {
        try
        {
            var cases = _db.LoadCases(_db.GetActiveSnapshotId());
            if (cases.Count == 0)
            {
                StatusMessage = "پرونده‌ای برای خروجی وجود ندارد";
                return;
            }

            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var exportDir = System.IO.Path.Combine(exeDir, "exports");
            System.IO.Directory.CreateDirectory(exportDir);

            var fileName = $"Cases_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            var filePath = System.IO.Path.Combine(exportDir, fileName);

            ExcelExporter.GenerateFullWorkbook(cases, filePath);

            LastExportPath = filePath;
            StatusMessage = $"فایل ایجاد شد: {fileName}";
            LoadFiles();
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطا: {ex.Message}";
        }
    }

    private void OpenFolder()
    {
        var exeDir = AppDomain.CurrentDomain.BaseDirectory;
        var exportDir = System.IO.Path.Combine(exeDir, "exports");
        if (System.IO.Directory.Exists(exportDir))
            System.Diagnostics.Process.Start("explorer.exe", exportDir);
    }
}
