using System.Collections.ObjectModel;
using System.Windows.Input;
using NezamMonitor.Core.Data;

namespace NezamMonitor.App.ViewModels;

public sealed class HistoryViewModel : ViewModelBase
{
    private readonly NezamDatabase _db;
    private long _activeSnapshotId;
    private string _statusMessage = "";

    public ObservableCollection<SnapshotItem> Snapshots { get; } = new();
    public long ActiveSnapshotId { get => _activeSnapshotId; set { SetProperty(ref _activeSnapshotId, value); } }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public ICommand SetActiveCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand MergeCommand { get; }

    public HistoryViewModel(NezamDatabase db)
    {
        _db = db;
        SetActiveCommand = new RelayCommand(() => { });
        DeleteCommand = new RelayCommand(() => { });
        MergeCommand = new RelayCommand(() => { });
        LoadSnapshots();
    }

    public void SetActive(long snapshotId)
    {
        _db.SetActiveSnapshot(snapshotId);
        ActiveSnapshotId = snapshotId;
        foreach (var s in Snapshots) s.IsActive = s.Id == snapshotId;
        StatusMessage = $"اسنپ‌شات #{snapshotId} به عنوان فعال انتخاب شد";
    }

    public void DeleteSnapshot(long snapshotId)
    {
        if (ActiveSnapshotId == snapshotId)
        {
            StatusMessage = "❌ نمی‌توان اسنپ‌شات فعال را حذف کرد";
            return;
        }

        _db.DeleteSnapshot(snapshotId);
        LoadSnapshots();
        StatusMessage = $"اسنپ‌شات #{snapshotId} حذف شد ✓";
    }

    public string MergeSnapshots()
    {
        var selected = Snapshots.Where(s => s.IsSelectedForMerge).ToList();
        if (selected.Count < 2)
        {
            StatusMessage = "❌ حداقل ۲ اسنپ‌شات انتخاب کنید";
            return StatusMessage;
        }

        var ids = selected.Select(s => s.Id).ToList();
        var newId = _db.MergeSnapshots(ids);
        LoadSnapshots();

        var mergedCount = Snapshots.FirstOrDefault(s => s.Id == newId)?.CaseCount ?? 0;
        StatusMessage = $"ادغام موفق ✓ → اسنپ‌شات #{newId} ({mergedCount} پرونده)";
        return StatusMessage;
    }

    private void LoadSnapshots()
    {
        Snapshots.Clear();
        ActiveSnapshotId = _db.GetActiveSnapshotId();
        var snapshots = _db.GetSnapshots();
        foreach (var s in snapshots)
            Snapshots.Add(new SnapshotItem
            {
                Id = s.Id,
                CreatedAt = s.CreatedAt,
                Status = s.Status,
                CaseCount = s.CaseCount,
                IsActive = s.Id == ActiveSnapshotId
            });
        StatusMessage = $"{Snapshots.Count} اسنپ‌شات موجود است";
    }
}

public sealed class SnapshotItem : ViewModelBase
{
    public long Id { get; set; }
    public string CreatedAt { get; set; } = "";
    public string Status { get; set; } = "";
    public int CaseCount { get; set; }
    private bool _isActive;
    public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }
    private bool _isSelectedForMerge;
    public bool IsSelectedForMerge { get => _isSelectedForMerge; set => SetProperty(ref _isSelectedForMerge, value); }
    public string DisplayText => $"#{Id} | {CreatedAt} | {CaseCount} پرونده {(IsActive ? "← فعلی" : "")}";
}
