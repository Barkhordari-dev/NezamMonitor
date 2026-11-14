using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using NezamMonitorPro.Core.Data;
using NezamMonitorPro.Core.Models;

namespace NezamMonitorPro.App.ViewModels;

/// <summary>
/// صفحه پیگیری - نمایش اطلاعات پرونده‌ها با تعداد گزارش‌های ثبت شده
/// </summary>
public sealed class FollowUpViewModel : ViewModelBase
{
    private readonly NezamDatabase _db;
    private bool _groupByRange = false;

    // L-016 / DUP-003: وضعیت فیلتر در سرویس مشترک FilterState است (همان
    // Cases و Fees). جستجو و فیلترها فقط پیش‌نویس‌اند؛ جدول تا دکمه «اعمال»
    // فیلتر نمی‌شود. GroupByRange فوری می‌ماند (دسته‌بندی است، نه فیلتر).
    public Services.FilterState Filters { get; } = new();
    /// <summary>True وقتی فیلتری انتخاب شده ولی «اعمال» هنوز زده نشده.</summary>
    public bool HasPendingFilters => Filters.HasPendingChanges;
    public string SearchText
    {
        get => Filters.GetDraft("Search", "");
        set { Filters.SetDraft("Search", value); OnPropertyChanged(nameof(SearchText)); OnPropertyChanged(nameof(HasPendingFilters)); }
    }

    public string FilterOwner
    {
        get => Filters.GetDraft("Owner");
        set { Filters.SetDraft("Owner", value); OnPropertyChanged(nameof(FilterOwner)); OnPropertyChanged(nameof(HasPendingFilters)); }
    }

    public string FilterCaseNumber
    {
        get => Filters.GetDraft("CaseNumber");
        set { Filters.SetDraft("CaseNumber", value); OnPropertyChanged(nameof(FilterCaseNumber)); OnPropertyChanged(nameof(HasPendingFilters)); }
    }

    public bool GroupByRange
    {
        get => _groupByRange;
        set { if (SetProperty(ref _groupByRange, value)) ApplyFilters(); }
    }

    public ObservableCollection<FollowUpItem> Items { get; } = new();
    public ObservableCollection<string> DistinctOwners { get; } = new();
    public ObservableCollection<string> DistinctCaseNumbers { get; } = new();

    public string CountDisplay => $"تعداد: {Items.Count} مورد";
    public bool HasActiveFilters => Filters.HasActiveFilters;

    public ICommand RefreshCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand GroupByRangeCommand { get; }
    public ICommand ExportVCardCommand { get; }
    public ICommand ApplyFiltersCommand { get; }
    public ICommand ClearFiltersCommand { get; }

    public FollowUpViewModel(NezamDatabase db)
    {
        _db = db;
        Filters.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasActiveFilters));
            OnPropertyChanged(nameof(HasPendingFilters));
        };
        RefreshCommand = new RelayCommand(() => { SaveDirtyItems(); LoadData(); });
        SaveCommand = new RelayCommand(ExecuteSave);
        GroupByRangeCommand = new RelayCommand(() => { GroupByRange = !GroupByRange; });
        ExportVCardCommand = new RelayCommand(() => ExportVCard());
        ApplyFiltersCommand = new RelayCommand(ApplyFilters);
        ClearFiltersCommand = new RelayCommand(ClearFilters);
        LoadData();
    }

    private void LoadData()
    {
        var cases = _db.LoadCases(_db.GetActiveSnapshotId());
        
        // دریافت ویرایش‌های ذخیره شده
        var savedEdits = _db.GetAllFollowUpEdits();

        // شمارش تعداد گزارش‌ها بر اساس تعداد ردیف گزارش‌های ثبت شده در هر پرونده
        var reportCounts = new Dictionary<string, int>();
        foreach (var c in cases)
        {
            reportCounts[c.CaseNumber] = c.Reports.Count;
        }

        DistinctOwners.Clear();
        DistinctOwners.Add("همه");
        DistinctCaseNumbers.Clear();
        DistinctCaseNumbers.Add("همه");

        var allItems = new List<FollowUpItem>();
        int row = 1;
        foreach (var c in cases)
        {
            var reportCount = reportCounts.TryGetValue(c.CaseNumber, out var cnt) ? cnt : 0;
            
            // بررسی وجود ویرایش ذخیره شده
            var hasEdit = savedEdits.TryGetValue(c.CaseNumber, out var edit);

            var item = new FollowUpItem
            {
                RowId = row++,
                CaseNumber = hasEdit && !string.IsNullOrEmpty(edit.CaseNumberEdit) ? edit.CaseNumberEdit : c.CaseNumber,
                Owner = hasEdit && !string.IsNullOrEmpty(edit.OwnerEdit) ? edit.OwnerEdit : c.Owner,
                Address = hasEdit && !string.IsNullOrEmpty(edit.AddressEdit) ? edit.AddressEdit : (c.Specification?.Address ?? ""),
                OwnerMobile = hasEdit && !string.IsNullOrEmpty(edit.OwnerMobileEdit) ? edit.OwnerMobileEdit : c.OwnerMobile,
                ReportCount = reportCount,
                Description = hasEdit ? edit.Description : "",
                // مقادیر اصلی برای تشخیص تغییر (همیشه از دیتابیس)
                OriginalCaseNumber = c.CaseNumber,
                OriginalOwner = c.Owner,
                OriginalAddress = c.Specification?.Address ?? "",
                OriginalOwnerMobile = c.OwnerMobile
            };
            allItems.Add(item);
        }

        // استخراج مقادیر منحصربفرد
        foreach (var owner in allItems.Select(i => i.Owner).Distinct().OrderBy(o => o))
            DistinctOwners.Add(owner);
        foreach (var cn in allItems.Select(i => i.CaseNumber).Distinct().OrderBy(c => c))
            DistinctCaseNumbers.Add(cn);

        // ذخیره لیست کامل
        _allItems = allItems;
        ApplyFilters();
    }

    private List<FollowUpItem> _allItems = new();

    private void ApplyFilters()
    {
        // فیلتر با مقدارهای اعمال‌شده خوانده می‌شود، نه پیش‌نویس.
        Filters.Apply();
        var appliedSearch = Filters.GetApplied("Search", "");
        var appliedOwner = Filters.GetApplied("Owner");
        var appliedCaseNumber = Filters.GetApplied("CaseNumber");

        Items.Clear();
        var filtered = _allItems.AsEnumerable();

        // فیلتر متن
        if (!string.IsNullOrWhiteSpace(appliedSearch))
        {
            var normSearch = FilterHelper.Normalize(appliedSearch);
            filtered = filtered.Where(i =>
                FilterHelper.Normalize($"{i.CaseNumber} {i.Owner} {i.Address} {i.OwnerMobile}")
                    .Contains(normSearch, StringComparison.OrdinalIgnoreCase));
        }

        // فیلتر مالک
        if (appliedOwner != "همه")
        {
            filtered = filtered.Where(i => FilterHelper.Matches(i.Owner, appliedOwner));
        }

        // فیلتر شماره پرونده
        if (appliedCaseNumber != "همه")
        {
            filtered = filtered.Where(i => FilterHelper.Matches(i.CaseNumber, appliedCaseNumber));
        }

        // دسته‌بندی بر اساس محدوده
        if (GroupByRange)
        {
            filtered = filtered.OrderBy(i => ExtractRange(i.Address))
                               .ThenBy(i => i.Owner);
        }
        else
        {
            filtered = filtered.OrderBy(i => i.RowId);
        }

        foreach (var item in filtered)
            Items.Add(item);

        OnPropertyChanged(nameof(CountDisplay));
        OnPropertyChanged(nameof(HasActiveFilters));
    }

    private void ClearFilters()
    {
        Filters.Clear(("Search", ""), ("Owner", "همه"), ("CaseNumber", "همه"));
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(FilterOwner));
        OnPropertyChanged(nameof(FilterCaseNumber));
        ApplyFilters();
    }

    /// <summary>
    /// استخراج محدوده از آدرس برای دسته‌بندی
    /// </summary>
    private static string ExtractRange(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return "نامشخص";

        // استخراج کلمه کلیدی از آدرس برای دسته‌بندی
        // مثال: "خیابان امام، کوچه 5" → "خیابان امام"
        var parts = address.Split(new[] { "،", ",", "خیابان", "کوچه", "بلوار", "جاده" }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0)
            return parts[0].Trim();
        return address.Length > 20 ? address[..20] : address;
    }

    /// <summary>
    /// ذخیره ویرایش‌ها در دیتابیس (فقط تغییر یافته‌ها)
    /// </summary>
    public void SaveDirtyItems()
    {
        var dirtyItems = _allItems.Where(i => i.IsModified).ToList();
        foreach (var item in dirtyItems)
        {
            _db.SaveFollowUpEdit(
                item.OriginalCaseNumber,
                item.Description,
                item.CaseNumber != item.OriginalCaseNumber ? item.CaseNumber : "",
                item.Owner != item.OriginalOwner ? item.Owner : "",
                item.Address != item.OriginalAddress ? item.Address : "",
                item.OwnerMobile != item.OriginalOwnerMobile ? item.OwnerMobile : ""
            );
        }
    }

    /// <summary>
    /// اجرای ذخیره صریح با تایید موفقیت
    /// </summary>
    public void ExecuteSave()
    {
        try
        {
            SaveDirtyItems();
            MessageBox.Show("✅ تغییرات با موفقیت ذخیره شد.", "ذخیره", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            // TXT-010: متن خام استثنا به کاربر نشان داده نمی‌شود؛ جزییات فقط در لاگ.
            NezamMonitorPro.App.Services.ErrorLog.Write(ex, "FollowUpViewModel.ExecuteSave");
            MessageBox.Show("ذخیره انجام نشد. دوباره تلاش کن.", "ذخیره", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// بروزرسانی یک سلول از داده‌های پرونده‌ها (فقط از طریق راست کلیک)
    /// </summary>
    public void RefreshCellFromCases(FollowUpItem item, string propertyName)
    {
        var cases = _db.LoadCases(_db.GetActiveSnapshotId());
        var c = cases.FirstOrDefault(x => x.CaseNumber == item.OriginalCaseNumber);
        if (c == null) return;

        switch (propertyName)
        {
            case nameof(FollowUpItem.CaseNumber):
                item.CaseNumber = c.CaseNumber;
                break;
            case nameof(FollowUpItem.Owner):
                item.Owner = c.Owner;
                break;
            case nameof(FollowUpItem.Address):
                item.Address = c.Specification?.Address ?? "";
                break;
            case nameof(FollowUpItem.OwnerMobile):
                item.OwnerMobile = c.OwnerMobile;
                break;
        }
        
        // ذخیره فقط همین آیتم
        _db.SaveFollowUpEdit(
            item.OriginalCaseNumber,
            item.Description,
            item.CaseNumber != item.OriginalCaseNumber ? item.CaseNumber : "",
            item.Owner != item.OriginalOwner ? item.Owner : "",
            item.Address != item.OriginalAddress ? item.Address : "",
            item.OwnerMobile != item.OriginalOwnerMobile ? item.OwnerMobile : ""
        );
    }

    public void ExportVCard(HashSet<string>? selectedCases = null)
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "ذخیره فایل مخاطبین مالکین",
                Filter = "فایل vCard|*.vcf",
                DefaultExt = ".vcf",
                FileName = $"owners_contacts_{DateTime.Now:yyyyMMdd}.vcf"
            };

            if (dialog.ShowDialog() != true) return;

            var exporter = new NezamMonitorPro.Core.Export.OwnerVCardExporter(_db);
            var result = exporter.Export(dialog.FileName, selectedCases);

            if (result.Success)
                MessageBox.Show(
                    $"✅ {result.OwnerCount} مالک — فایل ذخیره شد:\n{result.OutputPath}",
                    "خروجی مخاطبین", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show(
                    "❌ " + string.Join("\n", result.Errors),
                    "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            // TXT-010: متن خام استثنا به کاربر نشان داده نمی‌شود؛ جزییات فقط در لاگ.
            NezamMonitorPro.App.Services.ErrorLog.Write(ex, "FollowUpViewModel.ExportVCard");
            MessageBox.Show("خروجی مخاطبین ساخته نشد. دوباره تلاش کن.", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

/// <summary>
/// آیتم پیگیری
/// </summary>
public class FollowUpItem : INotifyPropertyChanged
{
    private string _caseNumber = "";
    private string _owner = "";
    private string _address = "";
    private string _ownerMobile = "";
    private string _description = "";

    public int RowId { get; set; }

    public string CaseNumber
    {
        get => _caseNumber;
        set { _caseNumber = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsCaseNumberModified)); }
    }

    public string Owner
    {
        get => _owner;
        set { _owner = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsOwnerModified)); }
    }

    public string Address
    {
        get => _address;
        set { _address = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsAddressModified)); }
    }

    public string OwnerMobile
    {
        get => _ownerMobile;
        set { _ownerMobile = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsOwnerMobileModified)); }
    }

    /// <summary>
    /// توضیحات (قابل ویرایش، ذخیره می‌شود)
    /// </summary>
    public string Description
    {
        get => _description;
        set { _description = value; OnPropertyChanged(); }
    }

    public int ReportCount { get; set; }

    // مقادیر اصلی از پرونده‌ها
    public string OriginalCaseNumber { get; set; } = "";
    public string OriginalOwner { get; set; } = "";
    public string OriginalAddress { get; set; } = "";
    public string OriginalOwnerMobile { get; set; } = "";

    // تشخیص تغییر
    public bool IsCaseNumberModified => FilterHelper.Normalize(CaseNumber) != FilterHelper.Normalize(OriginalCaseNumber);
    public bool IsOwnerModified => FilterHelper.Normalize(Owner) != FilterHelper.Normalize(OriginalOwner);
    public bool IsAddressModified => FilterHelper.Normalize(Address) != FilterHelper.Normalize(OriginalAddress);
    public bool IsOwnerMobileModified => FilterHelper.Normalize(OwnerMobile) != FilterHelper.Normalize(OriginalOwnerMobile);
    public bool IsModified => IsCaseNumberModified || IsOwnerModified || IsAddressModified || IsOwnerMobileModified || !string.IsNullOrEmpty(Description);

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
