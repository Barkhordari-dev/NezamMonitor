using System.Collections.ObjectModel;
using System.Windows.Input;
using NezamMonitorPro.Core.Data;
using NezamMonitorPro.Core.Diff;
using NezamMonitorPro.Core.Models;

namespace NezamMonitorPro.App.ViewModels;

public sealed class ChangesViewModel : ViewModelBase
{
    private readonly NezamDatabase _db;
    private long _fromSnapshotId;
    private long _toSnapshotId;
    private long _viewSnapshotId;
    private string _statusMessage = "اسنپ‌شات‌ها را انتخاب و مقایسه کنید";
    private bool _compared;

    public ObservableCollection<SnapshotItem> Snapshots { get; } = new();
    public ObservableCollection<ChangeItem> Changes { get; } = new();
    public long FromSnapshotId { get => _fromSnapshotId; set => SetProperty(ref _fromSnapshotId, value); }
    public long ToSnapshotId { get => _toSnapshotId; set => SetProperty(ref _toSnapshotId, value); }
    /// <summary>Snapshot shown by the read-only viewer (P3).</summary>
    public long ViewSnapshotId { get => _viewSnapshotId; set => SetProperty(ref _viewSnapshotId, value); }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public string SummaryText { get; set; } = "";
    /// <summary>G-020: True وقتی مقایسه انجام شده و هیچ تغییری نیست (متن حالت خالی).</summary>
    public bool HasNoChanges => _compared && Changes.Count == 0;
    public ICommand CompareCommand { get; }
    public ICommand ViewSnapshotCommand { get; }

    public ChangesViewModel(NezamDatabase db)
    {
        _db = db;
        CompareCommand = new RelayCommand(Compare);
        ViewSnapshotCommand = new RelayCommand(ViewSnapshot);
        LoadSnapshots();
    }

    private void LoadSnapshots()
    {
        Snapshots.Clear();
        var snapshots = _db.GetSnapshots();
        foreach (var s in snapshots)
            Snapshots.Add(new SnapshotItem { Id = s.Id, CreatedAt = s.CreatedAt, CaseCount = s.CaseCount });
        if (snapshots.Count >= 2)
        {
            FromSnapshotId = snapshots[1].Id;
            ToSnapshotId = snapshots[0].Id;
        }
        if (snapshots.Count >= 1)
            ViewSnapshotId = snapshots[0].Id;
    }

    /// <summary>Opens the selected snapshot in a read-only viewer (P3).
    /// The active snapshot is never changed and nothing is written.</summary>
    private void ViewSnapshot()
    {
        if (ViewSnapshotId <= 0)
        {
            StatusMessage = "اسنپ‌شاتی انتخاب نشده";
            return;
        }
        var window = new NezamMonitorPro.App.Views.SnapshotViewerWindow(new SnapshotViewerViewModel(_db, ViewSnapshotId));
        window.Show();
    }

    private void Compare()
    {
        // G-020: مقایسه یک اسنپ‌شات با خودش همیشه «۰ تغییر» می‌دهد و گمراه‌کننده است.
        if (FromSnapshotId <= 0 || ToSnapshotId <= 0)
        {
            StatusMessage = "اول هر دو اسنپ‌شات را انتخاب کن.";
            return;
        }
        if (FromSnapshotId == ToSnapshotId)
        {
            StatusMessage = "«از» و «تا» یکی‌اند؛ دو اسنپ‌شات متفاوت انتخاب کن.";
            return;
        }
        Changes.Clear();
        _compared = false;
        OnPropertyChanged(nameof(HasNoChanges));
        try
        {
            var casesA = _db.LoadCases(FromSnapshotId);
            var casesB = _db.LoadCases(ToSnapshotId);
            var result = SnapshotComparer.Compare(casesA, casesB);

            SummaryText = $"پرونده‌های جدید: {result.Added.Count} | حذف شده: {result.Removed.Count} | تغییر یافته: {result.Modified.Count} | بدون تغییر: {result.Unchanged.Count}";

            foreach (var fc in result.FieldChanges)
                Changes.Add(new ChangeItem
                {
                    CaseNumber = fc.CaseNumber,
                    Owner = fc.Owner,
                    // G-019: نام انگلیسی enum («Modified») به فارسی نگاشت می‌شود.
                    ChangeType = ChangeTypeDisplay(fc.Type),
                    Field = fc.Field,
                    OldValue = fc.OldValue,
                    NewValue = fc.NewValue
                });

            foreach (var added in result.Added)
                Changes.Add(new ChangeItem { CaseNumber = added.CaseNumber, Owner = added.Owner, ChangeType = "جدید", Field = "-", NewValue = "اضافه شده" });

            foreach (var removed in result.Removed)
                Changes.Add(new ChangeItem { CaseNumber = removed.CaseNumber, Owner = removed.Owner, ChangeType = "حذف شده", Field = "-", OldValue = "حذف شده" });

            _compared = true;
            OnPropertyChanged(nameof(HasNoChanges));
            StatusMessage = Changes.Count == 0
                ? "هیچ تغییری بین این دو اسنپ‌شات نیست."
                : $"{Changes.Count} تغییر شناسایی شد";
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطا: {ex.Message}";
        }
    }

    /// <summary>نگاشت فارسی نوع تغییر (به‌جای ToString روی enum انگلیسی).</summary>
    private static string ChangeTypeDisplay(ChangeType type) => type switch
    {
        ChangeType.Added => "جدید",
        ChangeType.Removed => "حذف شده",
        ChangeType.Modified => "ویرایش شده",
        ChangeType.Unchanged => "بدون تغییر",
        _ => "تغییر کرده"
    };
}

public sealed class ChangeItem
{
    public string CaseNumber { get; set; } = "";
    public string Owner { get; set; } = "";
    public string ChangeType { get; set; } = "";
    public string Field { get; set; } = "";
    public string OldValue { get; set; } = "";
    public string NewValue { get; set; } = "";
}
