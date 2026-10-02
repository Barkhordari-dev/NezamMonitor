using System.Collections.ObjectModel;
using System.Windows.Input;
using NezamMonitor.Core.Data;
using NezamMonitor.Core.Diff;
using NezamMonitor.Core.Models;

namespace NezamMonitor.App.ViewModels;

public sealed class ChangesViewModel : ViewModelBase
{
    private readonly NezamDatabase _db;
    private long _fromSnapshotId;
    private long _toSnapshotId;
    private string _statusMessage = "اسنپ‌شات‌ها را انتخاب و مقایسه کنید";

    public ObservableCollection<SnapshotItem> Snapshots { get; } = new();
    public ObservableCollection<ChangeItem> Changes { get; } = new();
    public long FromSnapshotId { get => _fromSnapshotId; set => SetProperty(ref _fromSnapshotId, value); }
    public long ToSnapshotId { get => _toSnapshotId; set => SetProperty(ref _toSnapshotId, value); }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public string SummaryText { get; set; } = "";
    public ICommand CompareCommand { get; }

    public ChangesViewModel(NezamDatabase db)
    {
        _db = db;
        CompareCommand = new RelayCommand(Compare);
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
    }

    private void Compare()
    {
        Changes.Clear();
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
                    ChangeType = fc.Type.ToString(),
                    Field = fc.Field,
                    OldValue = fc.OldValue,
                    NewValue = fc.NewValue
                });

            foreach (var added in result.Added)
                Changes.Add(new ChangeItem { CaseNumber = added.CaseNumber, Owner = added.Owner, ChangeType = "جدید", Field = "-", NewValue = "اضافه شده" });

            foreach (var removed in result.Removed)
                Changes.Add(new ChangeItem { CaseNumber = removed.CaseNumber, Owner = removed.Owner, ChangeType = "حذف شده", Field = "-", OldValue = "حذف شده" });

            StatusMessage = $"{Changes.Count} تغییر شناسایی شد";
        }
        catch (Exception ex)
        {
            StatusMessage = $"خطا: {ex.Message}";
        }
    }
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
