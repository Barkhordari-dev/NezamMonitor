using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NezamMonitorPro.App.ViewModels;

/// <summary>
/// آیتم وضعیت تولید گزارش (در جدول اسکن)
/// </summary>
public class CaseReportItem : INotifyPropertyChanged
{
    private bool _isSelected;
    private string _status = "";
    private string _generatedAt = "";
    private string _generatedAtShamsi = "";
    private bool _isChanged;

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public string CaseNumber { get; set; } = "";
    public string PermitNumber { get; set; } = "";
    public int RowNumber { get; set; }
    public string Owner { get; set; } = "";
    public string FullAddress { get; set; } = "";
    public string Companion { get; set; } = "";
    public int Stage { get; set; }
    public string StageName { get; set; } = "";

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public string GeneratedAt
    {
        get => _generatedAt;
        set { _generatedAt = value; OnPropertyChanged(); }
    }
    public string GeneratedAtShamsi
    {
        get => _generatedAtShamsi;
        set { _generatedAtShamsi = value; OnPropertyChanged(); }
    }

    /// <summary>Server report date (Jalali, from the سامانه) — empty when not in the سامانه.</summary>
    public string ServerDate { get; set; } = "";
    /// <summary>True when this (case, stage) report is registered in the سامانه.</summary>
    public bool IsServerReport { get; set; }

    public bool IsChanged
    {
        get => _isChanged;
        set { _isChanged = value; OnPropertyChanged(); }
    }

    public string PermitDate { get; set; } = "";
    public string Deadline { get; set; } = "";
    public string DaysRemaining { get; set; } = "";
    /// <summary>مقدار عددی برای مرتب‌سازی ستون «مانده»: منفی=گذشته، ۰=امروز، مثبت=آینده، int.MaxValue=بدون مهلت</summary>
    public int DaysRemainingSort { get; set; } = int.MaxValue;

    private string _deadlineStatus = "";
    public string DeadlineStatus
    {
        get => _deadlineStatus;
        set { if (_deadlineStatus != value) { _deadlineStatus = value; OnPropertyChanged(); OnPropertyChanged(nameof(DeadlineStatusText)); } }
    }

    /// <summary>کلمه کوتاه کنار ایموجی هشدار: گذشته / نزدیک / عادی / تولیدشده.</summary>
    public string DeadlineStatusText => DeadlineStatus switch
    {
        "✅" => "تولیدشده",
        "🔴" => "گذشته",
        "⚠️" => "نزدیک",
        "📅" => "عادی",
        _ => ""
    };

    public string BuildingGroup { get; set; } = "";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
