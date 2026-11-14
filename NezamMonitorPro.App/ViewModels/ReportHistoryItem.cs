using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.IO;

namespace NezamMonitorPro.App.ViewModels;

/// <summary>
/// آیتم تاریخچه گزارش‌های تولیدشده
/// </summary>
public class ReportHistoryItem : INotifyPropertyChanged
{
    public int RowId { get; set; }
    public string CaseNumber { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public string Owner { get; set; } = "";
    public int Stage { get; set; }
    public string StageName { get; set; } = "";
    public string ReportType { get; set; } = "";
    public string ReportDate { get; set; } = "";
    public string NumberFormat { get; set; } = "";
    public string OutputPath { get; set; } = "";
    public string PdfPath { get; set; } = "";
    public string FileType { get; set; } = "word";
    /// <summary>نوع فایل برای نمایش در سابقه (V5).</summary>
    public string FileKindDisplay => FileType == "pdf" ? "📄 PDF" : "📝 Word";
    public bool HasPdf => FileType == "pdf" && !string.IsNullOrEmpty(PdfPath);
    public string FileName => Path.GetFileName(OutputPath) ?? "";
    public string Status { get; set; } = "";
    public bool FileExists { get; set; }
    public string FolderPath { get; set; } = "";
    public string FolderName { get; set; } = "";

    private bool _isSelected;
    /// <summary>Row checkbox (history tab) — the batch operations act on the checked rows.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
