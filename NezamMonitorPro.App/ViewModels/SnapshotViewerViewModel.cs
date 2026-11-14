using System.Collections.ObjectModel;
using NezamMonitorPro.Core.Data;
using NezamMonitorPro.Core.Models;

namespace NezamMonitorPro.App.ViewModels;

/// <summary>
/// Read-only viewer for one snapshot (P3). Nothing here writes to the
/// database and the active snapshot is never changed — it only shows what
/// that snapshot contained at extraction time.
/// </summary>
public sealed class SnapshotViewerViewModel : ViewModelBase
{
    private Case? _selectedCase;

    public long SnapshotId { get; }
    public string Title { get; }
    public string SummaryText { get; }
    public ObservableCollection<Case> Cases { get; } = new();
    public Case? SelectedCase { get => _selectedCase; set => SetProperty(ref _selectedCase, value); }

    public SnapshotViewerViewModel(NezamDatabase db, long snapshotId)
    {
        SnapshotId = snapshotId;
        var snapshots = db.GetSnapshots();
        var snap = snapshots.FirstOrDefault(s => s.Id == snapshotId);
        var cases = db.LoadCases(snapshotId);
        foreach (var c in cases) Cases.Add(c);
        Title = $"اسنپ‌شات {snap.CreatedAt} — {cases.Count} پرونده (فقط‌خواندنی)";
        SummaryText = $"پرونده‌ها: {cases.Count} | مهندسین: {cases.Sum(c => c.Engineers.Count)} | حق‌الزحمه: {cases.Sum(c => c.Fees.Count)} | گزارش‌ها: {cases.Sum(c => c.Reports.Count)}";
        SelectedCase = Cases.FirstOrDefault();
    }
}
