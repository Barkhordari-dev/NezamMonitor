using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using NezamMonitorPro.App.Services;
using NezamMonitorPro.Core;
using NezamMonitorPro.Core.Data;

namespace NezamMonitorPro.App.ViewModels;

public sealed class HistoryViewModel : ViewModelBase
{
    private readonly NezamDatabase _db;
    private long _activeSnapshotId;
    private string _statusMessage = "";
    private string _legacyStatus = "";

    public ObservableCollection<SnapshotItem> Snapshots { get; } = new();

    /// <summary>پایگاه‌داده‌های نسخه‌های قبلی که فقط برای مرور/بایگانی افزوده شده‌اند.</summary>
    public ObservableCollection<LegacyArchiveItem> LegacyDatabases { get; } = new();

    /// <summary>پایگاه‌داده‌های قدیمی که کنار فایل اجرایی پیدا شده‌اند (هنوز بایگانی نشده‌اند).</summary>
    public ObservableCollection<LegacyArchiveItem> DetectedDatabases { get; } = new();

    public long ActiveSnapshotId { get => _activeSnapshotId; set => SetProperty(ref _activeSnapshotId, value); }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public string LegacyStatus { get => _legacyStatus; set => SetProperty(ref _legacyStatus, value); }
    public HistoryViewModel(NezamDatabase db)
    {
        _db = db;

        // هر بخش مستقل و محافظت‌شده است تا خطای یک بخش، کل صفحه را از کار نیندازد
        SafeLoadSnapshots();
        SafeRefreshLegacy();
        SafeScanCandidates();
    }

    // ═══════════════ اسنپ‌شات‌ها ═══════════════

    /// <summary>تازه‌سازی کامل صفحه هنگام بازگشت به آن (ویو کش می‌شود).</summary>
    public void RefreshAll()
    {
        SafeLoadSnapshots();
        SafeRefreshLegacy();
        SafeScanCandidates();
    }

    private void SafeLoadSnapshots()
    {
        try
        {
            LoadSnapshots();
        }
        catch (Exception ex)
        {
            StatusMessage = "خواندن اسنپ‌شات‌ها ناموفق بود: " + ex.Message;
        }
    }

    private void LoadSnapshots()
    {
        Snapshots.Clear();
        ActiveSnapshotId = _db.GetActiveSnapshotId();
        var snapshots = _db.GetSnapshots();
        foreach (var s in snapshots)
        {
            Snapshots.Add(new SnapshotItem
            {
                Id = s.Id,
                CreatedAt = s.CreatedAt,
                Status = s.Status,
                CaseCount = s.CaseCount,
                IsActive = s.Id == ActiveSnapshotId
            });
        }
        StatusMessage = $"{Snapshots.Count} اسنپ‌شات موجود است";
    }

    public void SetActive(long snapshotId)
    {
        var snap = Snapshots.FirstOrDefault(s => s.Id == snapshotId);
        if (snap == null) return;

        // فعال‌سازی اسنپ‌شات، پایگاه‌داده فعال کل برنامه را عوض می‌کند؛ پس حتما تایید می‌خواهد.
        var confirm = MessageBox.Show(
            $"این اسنپ‌شات، پایگاه‌داده فعال کل برنامه می‌شود و همه صفحه‌ها از این به بعد با پرونده‌های آن کار می‌کنند.\n\nتاریخ: {snap.CreatedAt}\nتعداد پرونده: {snap.CaseCount}\n\nمطمینی؟",
            "تایید فعال‌سازی", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            _db.SetActiveSnapshot(snapshotId);
            ActiveSnapshotId = snapshotId;
            foreach (var s in Snapshots) s.IsActive = s.Id == snapshotId;
            StatusMessage = $"✅ اسنپ‌شات شماره {snapshotId} فعال شد؛ از حالا همه صفحه‌ها با پرونده‌های این اسنپ‌شات کار می‌کنند.";
        }
        catch (Exception ex)
        {
            StatusMessage = "انتخاب اسنپ‌شات فعال ناموفق بود: " + ex.Message;
        }
    }

    public void DeleteSnapshot(long snapshotId)
    {
        try
        {
            if (ActiveSnapshotId == snapshotId)
            {
                StatusMessage = "❌ نمی‌توان اسنپ‌شات فعال را حذف کرد";
                return;
            }

            _db.DeleteSnapshot(snapshotId);
            SafeLoadSnapshots();
            StatusMessage = $"اسنپ‌شات #{snapshotId} حذف شد ✓";
        }
        catch (Exception ex)
        {
            StatusMessage = "حذف اسنپ‌شات ناموفق بود: " + ex.Message;
        }
    }

    public string MergeSnapshots()
    {
        try
        {
            var selected = Snapshots.Where(s => s.IsSelectedForMerge).ToList();
            if (selected.Count < 2)
            {
                StatusMessage = "❌ حداقل ۲ اسنپ‌شات انتخاب کنید";
                return StatusMessage;
            }

            var ids = selected.Select(s => s.Id).ToList();
            var newId = _db.MergeSnapshots(ids);
            SafeLoadSnapshots();

            var mergedCount = Snapshots.FirstOrDefault(s => s.Id == newId)?.CaseCount ?? 0;
            StatusMessage = $"ادغام موفق ✓ → اسنپ‌شات #{newId} ({mergedCount} پرونده)";
            return StatusMessage;
        }
        catch (Exception ex)
        {
            StatusMessage = "ادغام ناموفق بود: " + ex.Message;
            return StatusMessage;
        }
    }

    // ═══════════════ بایگانی نسخه‌های قبلی ═══════════════

    private void SafeRefreshLegacy()
    {
        try
        {
            RefreshLegacy();
        }
        catch (Exception ex)
        {
            LegacyStatus = "خواندن بایگانی ناموفق بود: " + ex.Message;
        }
    }

    public void RefreshLegacy()
    {
        LegacyDatabases.Clear();
        foreach (var item in LegacyDatabaseArchive.List())
            LegacyDatabases.Add(item);

        LegacyStatus = LegacyDatabases.Count == 0
            ? "هنوز پایگاه‌داده نسخه قبلی افزوده نشده است."
            : $"{LegacyDatabases.Count} پایگاه‌داده بایگانی‌شده";
    }

    private void SafeScanCandidates()
    {
        try
        {
            ScanCandidates();
        }
        catch
        {
        }
    }

    /// <summary>جستجوی خودکار فایل‌های *.db کنار برنامه (مثل nezam_monitor.db قدیمی).</summary>
    public void ScanCandidates()
    {
        DetectedDatabases.Clear();
        foreach (var item in LegacyDatabaseArchive.ScanCandidates())
            DetectedDatabases.Add(item);

        if (DetectedDatabases.Count > 0)
            LegacyStatus = $"{DetectedDatabases.Count} پایگاه‌داده قدیمی کنار برنامه پیدا شد.";
    }

    /// <summary>افزودن یک مورد پیداشده (مسیر فایل) به بایگانی؛ فایل اصلی دست‌نخورده می‌ماند.</summary>
    public string AttachDetected(string sourcePath)
    {
        var status = AttachLegacy(sourcePath);
        SafeScanCandidates();
        return status;
    }

    /// <summary>افزودن یک کپی از پایگاه‌داده قدیمی به بایگانی؛ پایگاه‌داده فعال دست‌نخورده می‌ماند.</summary>
    public string AttachLegacy(string sourcePath)
    {
        try
        {
            var item = LegacyDatabaseArchive.Attach(sourcePath);
            SafeRefreshLegacy();
            LegacyStatus = item.IsReadable
                ? $"افزوده شد: {item.Name} ({item.Summary})"
                : $"بایگانی شد (خواندن ممکن نیست): {item.Name}";
            return LegacyStatus;
        }
        catch (Exception ex)
        {
            LegacyStatus = "افزودن ناموفق: " + ex.Message;
            return LegacyStatus;
        }
    }

    /// <summary>
    /// ایمپورت داده‌های یک پایگاه‌داده نسخه قبلی داخل پایگاه‌داده رمزنگاری‌شده فعلی.
    /// فایل قدیمی فقط‌خواندنی است و تغییر نمی‌کند؛ داده‌ها در یک اسنپ‌شات جدید ذخیره می‌شوند.
    /// اجرا به صورت Async جهت جلوگیری از قفل شدن UI انجام می‌شود.
    /// </summary>
    public async Task<string> ImportLegacyAsync(string sourcePath)
    {
        try
        {
            LegacyStatus = "در حال ایمپورت داده‌ها... لطفا شکیبا باشید.";
            var result = await Task.Run(() => LegacyDatabaseImporter.Import(_db, sourcePath));
            SafeLoadSnapshots();
            LegacyStatus = result.Message;
            return result.Message;
        }
        catch (Exception ex)
        {
            LegacyStatus = "ایمپورت ناموفق بود: " + ex.Message;
            return LegacyStatus;
        }
    }

    public string ImportLegacy(string sourcePath)
    {
        return ImportLegacyAsync(sourcePath).GetAwaiter().GetResult();
    }

    public void RemoveLegacy(LegacyArchiveItem item)    {
        try
        {
            LegacyDatabaseArchive.Remove(item.Path);
            SafeRefreshLegacy();
            LegacyStatus = $"از بایگانی حذف شد: {item.Name}";
        }
        catch (Exception ex)
        {
            LegacyStatus = "حذف از بایگانی ناموفق بود: " + ex.Message;
        }
    }

}

public sealed class SnapshotItem : ViewModelBase
{
    public long Id { get; set; }
    public string CreatedAt { get; set; } = "";
    public string Status { get; set; } = "";
    public int CaseCount { get; set; }
    private bool _isActive;
    public bool IsActive { get => _isActive; set { SetProperty(ref _isActive, value); OnPropertyChanged(nameof(DisplayText)); OnPropertyChanged(nameof(CanDelete)); OnPropertyChanged(nameof(DeleteToolTip)); } }

    /// <summary>اسنپ‌شات فعال را نمی‌شود حذف کرد.</summary>
    public bool CanDelete => !IsActive;

    /// <summary>توضیح دکمه حذف؛ برای اسنپ‌شات فعال دلیل غیرفعال بودنش را می‌گوید.</summary>
    public string DeleteToolTip => IsActive
        ? "اسنپ‌شات فعال را نمی‌شود حذف کرد؛ اول یک اسنپ‌شات دیگر را فعال کن."
        : "حذف این اسنپ‌شات (با تایید).";
    private bool _isSelectedForMerge;
    public bool IsSelectedForMerge { get => _isSelectedForMerge; set => SetProperty(ref _isSelectedForMerge, value); }
    // All digits in this column must be English (Latin) digits, even when the
    // stored CreatedAt string or the fa-IR thread culture carries Persian
    // digits (old snapshots were saved that way).
    public string DisplayText => DigitNormalizer.ToAsciiDigits(
        $"#{Id} | {CreatedAt} | {CaseCount} پرونده {(IsActive ? "← فعلی" : "")}");
}
