using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using NezamMonitorPro.App.Services;
using NezamMonitorPro.Core.Data;
using NezamMonitorPro.Security.Services;

namespace NezamMonitorPro.App.ViewModels;

/// <summary>
/// Hidden menu (mirrors Android's hidden settings): data overrides, UI
/// customisation, database tools and the danger zone. Only reachable after
/// the 10-tap + password unlock; the unlock is session-only.
/// </summary>
public sealed class HiddenSettingsViewModel : ViewModelBase
{
    private readonly NezamDatabase _db;

    public event Action? RequestClose;

    // ── Overrides tab ────────────────────────────────────────────────

    public sealed class OverrideRow
    {
        public required HiddenOverride Override { get; init; }
        public string TypeDisplay { get; init; } = "";
        public string FieldDisplay { get; init; } = "";
        public string ModeDisplay { get; init; } = "";
    }

    public sealed record TypeOption(string Display, string Key);
    public sealed record FieldOption(string Display, string Name, bool IsCheckbox);

    public List<TypeOption> RecordTypes { get; } =
    [
        new("پرونده", "case"),
        new("حق‌الزحمه", "fee"),
        new("مهندس", "engineer")
    ];

    private static readonly Dictionary<string, List<FieldOption>> FieldsByType = new()
    {
        ["case"] =
        [
            new("مالک", "Owner", false),
            new("آدرس", "Address", false),
            new("همراه مالک", "OwnerMobile", false),
            new("بایگانی", "Archived", true)
        ],
        ["fee"] =
        [
            new("مبلغ", "Amount", false),
            new("وضعیت پرداخت", "PayStatus", false)
        ],
        ["engineer"] =
        [
            new("نام", "Name", false),
            new("تلفن", "Phone", false)
        ]
    };

    public ObservableCollection<OverrideRow> Overrides { get; } = new();
    private OverrideRow? _selectedOverride;
    public OverrideRow? SelectedOverride
    {
        get => _selectedOverride;
        set
        {
            // S-022: دکمه «حذف انتخاب‌شده» با تغییر انتخاب، فعال/غیرفعال می‌شود.
            if (SetProperty(ref _selectedOverride, value))
                (DeleteOverrideCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private TypeOption _selectedRecordType;
    public TypeOption SelectedRecordType
    {
        get => _selectedRecordType;
        set
        {
            if (SetProperty(ref _selectedRecordType, value))
            {
                FormFields.Clear();
                foreach (var f in FieldsByType[value.Key]) FormFields.Add(f);
                SelectedField = FormFields[0];
                OnPropertyChanged(nameof(ShowStageRow));
                OnPropertyChanged(nameof(ShowNameRow));
            }
        }
    }

    public ObservableCollection<FieldOption> FormFields { get; } = new();
    private FieldOption? _selectedField;
    public FieldOption? SelectedField
    {
        get => _selectedField;
        set
        {
            if (SetProperty(ref _selectedField, value))
            {
                OnPropertyChanged(nameof(FormFieldIsCheckbox));
                OnPropertyChanged(nameof(FormFieldIsText));
            }
        }
    }
    public bool FormFieldIsCheckbox => SelectedField?.IsCheckbox == true;
    public bool FormFieldIsText => !FormFieldIsCheckbox;
    public bool ShowStageRow => SelectedRecordType.Key == "fee";
    public bool ShowNameRow => SelectedRecordType.Key == "engineer";

    private string _formCaseNumber = "";
    public string FormCaseNumber { get => _formCaseNumber; set => SetProperty(ref _formCaseNumber, value); }
    private string _formStage = "1";
    public string FormStage { get => _formStage; set => SetProperty(ref _formStage, value); }
    public List<string> StageOptions { get; } = ["1", "2", "3", "4"];
    private string _formEngineerName = "";
    public string FormEngineerName { get => _formEngineerName; set => SetProperty(ref _formEngineerName, value); }
    private string _formValue = "";
    public string FormValue { get => _formValue; set => SetProperty(ref _formValue, value); }
    private bool _formIsArchived;
    public bool FormIsArchived { get => _formIsArchived; set => SetProperty(ref _formIsArchived, value); }
    private string _selectedModeDisplay = "موقت";
    public string SelectedModeDisplay { get => _selectedModeDisplay; set => SetProperty(ref _selectedModeDisplay, value); }
    public List<string> ModeOptions { get; } = ["دایمی", "موقت"];

    // ── UI customisation tab ─────────────────────────────────────────

    public sealed class NavItemRow : ViewModelBase
    {
        public required string Tag { get; init; }
        public required string DefaultTitle { get; init; }
        private string _title = "";
        public string Title { get => _title; set => SetProperty(ref _title, value); }
        private bool _visible = true;
        public bool Visible { get => _visible; set => SetProperty(ref _visible, value); }
    }

    public ObservableCollection<NavItemRow> NavItems { get; } = new();
    private string _displayName = "";
    public string DisplayName { get => _displayName; set => SetProperty(ref _displayName, value); }
    private bool _hideProBadge;
    public bool HideProBadge { get => _hideProBadge; set => SetProperty(ref _hideProBadge, value); }

    // ── Database tools tab ───────────────────────────────────────────

    public string DbPath { get; } = DatabaseService.DatabasePath;
    private string _dbInfo = "";
    public string DbInfo { get => _dbInfo; set => SetProperty(ref _dbInfo, value); }

    public sealed class RestorePoint
    {
        public required string FileName { get; init; }
        public required string FullPath { get; init; }
        public required string Info { get; init; }
    }

    public ObservableCollection<RestorePoint> RestorePoints { get; } = new();
    private RestorePoint? _selectedRestorePoint;
    public RestorePoint? SelectedRestorePoint
    {
        get => _selectedRestorePoint;
        set
        {
            // S-022: دکمه‌های «بازگردانی» و «حذف» با تغییر انتخاب، فعال/غیرفعال می‌شوند.
            if (SetProperty(ref _selectedRestorePoint, value))
                (RestorePointCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    // ── Common ───────────────────────────────────────────────────────

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    public System.Windows.Input.ICommand SaveOverrideCommand { get; }
    public System.Windows.Input.ICommand DeleteOverrideCommand { get; }
    public System.Windows.Input.ICommand ReapplyCommand { get; }
    public System.Windows.Input.ICommand SaveUiCommand { get; }
    public System.Windows.Input.ICommand ExportBackupCommand { get; }
    public System.Windows.Input.ICommand ImportBackupCommand { get; }
    public System.Windows.Input.ICommand ResetUiCommand { get; }
    public System.Windows.Input.ICommand CheckIntegrityCommand { get; }
    public System.Windows.Input.ICommand VacuumCommand { get; }
    public System.Windows.Input.ICommand CreateRestorePointCommand { get; }
    public System.Windows.Input.ICommand RestorePointCommand { get; }
    public System.Windows.Input.ICommand RefreshDbInfoCommand { get; }
    public System.Windows.Input.ICommand FactoryResetCommand { get; }
    public System.Windows.Input.ICommand DeleteGeneratedReportsCommand { get; }
    public System.Windows.Input.ICommand LogoutCommand { get; }
    public System.Windows.Input.ICommand ResetSettingsCommand { get; }
    public System.Windows.Input.ICommand LockNowCommand { get; }
    public System.Windows.Input.ICommand ResetAllHiddenCommand { get; }

    public HiddenSettingsViewModel(NezamDatabase db)
    {
        _db = db;
        _selectedRecordType = RecordTypes[0];
        foreach (var f in FieldsByType["case"]) FormFields.Add(f);
        SelectedField = FormFields[0];

        SaveOverrideCommand = new RelayCommand(SaveOverride);
        DeleteOverrideCommand = new RelayCommand(DeleteOverride, () => SelectedOverride != null);
        ReapplyCommand = new RelayCommand(Reapply);
        SaveUiCommand = new RelayCommand(SaveUi);
        ExportBackupCommand = new RelayCommand(ExportBackup);
        ImportBackupCommand = new RelayCommand(ImportBackup);
        ResetUiCommand = new RelayCommand(ResetUi);
        CheckIntegrityCommand = new RelayCommand(CheckIntegrity);
        VacuumCommand = new RelayCommand(Vacuum);
        CreateRestorePointCommand = new RelayCommand(CreateRestorePoint);
        RestorePointCommand = new RelayCommand(RestoreSelectedPoint, () => SelectedRestorePoint != null);
        RefreshDbInfoCommand = new RelayCommand(RefreshDbInfo);
        FactoryResetCommand = new RelayCommand(FactoryReset);
        DeleteGeneratedReportsCommand = new RelayCommand(DeleteGeneratedReports);
        LogoutCommand = new RelayCommand(Logout);
        ResetSettingsCommand = new RelayCommand(ResetSettings);
        LockNowCommand = new RelayCommand(LockNow);
        ResetAllHiddenCommand = new RelayCommand(ResetAllHidden);

        RefreshOverrides();
        LoadUi();
        RefreshDbInfo();
    }

    // ── Overrides ────────────────────────────────────────────────────

    private static string TypeDisplayOf(string type) => type switch
    {
        "case" => "پرونده",
        "fee" => "حق‌الزحمه",
        "engineer" => "مهندس",
        _ => type
    };

    private static string FieldDisplayOf(string type, string field)
    {
        if (FieldsByType.TryGetValue(type, out var list))
        {
            var match = list.FirstOrDefault(f => f.Name == field);
            if (match != null) return match.Display;
        }
        return field;
    }

    private void RefreshOverrides()
    {
        Overrides.Clear();
        foreach (var ov in _db.GetHiddenOverrides())
        {
            Overrides.Add(new OverrideRow
            {
                Override = ov,
                TypeDisplay = TypeDisplayOf(ov.RecordType),
                FieldDisplay = FieldDisplayOf(ov.RecordType, ov.Field),
                ModeDisplay = ov.Mode == "temp" ? "موقت" : "دایمی"
            });
        }
    }

    private void SaveOverride()
    {
        var cn = FormCaseNumber.Trim();
        if (string.IsNullOrWhiteSpace(cn)) { StatusMessage = "❌ شماره پرونده را وارد کن."; return; }
        if (SelectedField == null) { StatusMessage = "❌ فیلد را انتخاب کن."; return; }

        string key;
        if (SelectedRecordType.Key == "fee")
            key = $"{cn}|{FormStage}";
        else if (SelectedRecordType.Key == "engineer")
        {
            if (string.IsNullOrWhiteSpace(FormEngineerName)) { StatusMessage = "❌ نام مهندس را وارد کن."; return; }
            key = $"{cn}|{FormEngineerName.Trim()}";
        }
        else
            key = cn;

        var value = SelectedField.IsCheckbox ? (FormIsArchived ? "1" : "0") : FormValue;
        if (!SelectedField.IsCheckbox && string.IsNullOrWhiteSpace(value))
        {
            StatusMessage = "❌ مقدار تازه را وارد کن.";
            return;
        }

        var ov = new HiddenOverride
        {
            RecordType = SelectedRecordType.Key,
            RecordKey = key,
            Field = SelectedField.Name,
            Value = value.Trim(),
            Mode = SelectedModeDisplay == "موقت" ? "temp" : "perm",
            UpdatedAt = HiddenOverrideApplier.NowJalali()
        };
        try
        {
            _db.SaveHiddenOverride(ov);
            // ARCH-003: دسترسی مستقیم به اتصال مشترک زیر قفل اتصال انجام می‌شود.
            bool applied;
            lock (_db.SyncRoot)
            {
                applied = HiddenOverrideApplier.ApplyOne(_db.Connection, ov);
            }
            RefreshOverrides();
            StatusMessage = applied
                ? "✅ ذخیره و اعمال شد."
                : "✅ ذخیره شد، ولی ردیفش در پایگاه داده پیدا نشد؛ وقتی ردیف بیاید، خودش اعمال می‌شود.";
        }
        catch (Exception ex)
        {
            StatusMessage = "❌ ذخیره نشد: " + ex.Message;
        }
    }

    private void DeleteOverride()
    {
        if (SelectedOverride == null) return;
        // S-020: حذف تغییر تاییدیه می‌خواهد و معلوم است که مقدار دیتابیس برنمی‌گردد.
        var answer = MessageBox.Show(
            "تغییر انتخاب‌شده حذف می‌شود؛ مقدار فعلی دیتابیس برنمی‌گردد و برای برگرداندنش باید دوباره تغییر بسازی.\n\nادامه می‌دهی؟",
            "حذف تغییر",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        try
        {
            _db.DeleteHiddenOverride(SelectedOverride.Override.Id);
            RefreshOverrides();
            StatusMessage = "✅ تغییر حذف شد.";
        }
        catch (Exception ex)
        {
            StatusMessage = "❌ حذف نشد: " + ex.Message;
        }
    }

    private void Reapply()
    {
        try
        {
            // ARCH-003: دسترسی مستقیم به اتصال مشترک زیر قفل اتصال انجام می‌شود.
            lock (_db.SyncRoot)
            {
                HiddenOverrideApplier.ApplyAll(_db.Connection);
            }
            StatusMessage = "✅ تغییرهای دایمی دوباره اعمال شد.";
        }
        catch (Exception ex)
        {
            StatusMessage = "❌ اعمال نشد: " + ex.Message;
        }
    }

    // ── UI customisation ─────────────────────────────────────────────

    private void LoadUi()
    {
        var custom = UiCustomizationService.Load(_db);
        NavItems.Clear();
        foreach (var (tag, title) in UiCustomizationService.NavDefaults)
        {
            var item = custom.NavItems.TryGetValue(tag, out var saved) ? saved : null;
            NavItems.Add(new NavItemRow
            {
                Tag = tag,
                DefaultTitle = title,
                Title = item?.Title ?? title,
                Visible = item?.Visible ?? true
            });
        }
        DisplayName = custom.DisplayName;
        HideProBadge = custom.HideProBadge;
    }

    private UiCustomization BuildUiFromForm()
    {
        var custom = new UiCustomization { DisplayName = DisplayName.Trim(), HideProBadge = HideProBadge };
        foreach (var row in NavItems)
            custom.NavItems[row.Tag] = new NavItemCustom { Title = row.Title.Trim(), Visible = row.Visible };
        return custom;
    }

    private void ApplyUiToMainWindow()
    {
        try
        {
            if (System.Windows.Application.Current?.MainWindow is MainWindow main)
                main.ApplyUiCustomization();
        }
        catch { }
    }

    private void SaveUi()
    {
        UiCustomizationService.Save(_db, BuildUiFromForm());
        ApplyUiToMainWindow();
        StatusMessage = "✅ شخصی‌سازی ظاهر ذخیره و اعمال شد.";
    }

    private void ExportBackup()
    {
        var path = Path.Combine(DatabaseService.GetAppDirectory(), UiCustomizationService.BackupFileName);
        var (ok, message) = UiCustomizationService.ExportToFile(_db, path);
        StatusMessage = message;
    }

    private void ImportBackup()
    {
        // S-021: ورودی JSON تاییدیه می‌خواهد و اول یک پشتیبان خودکار می‌سازد.
        if (!ConfirmDanger("تنظیم‌های فعلی ظاهر با تنظیم‌های فایل hidden-settings-backup.json جایگزین می‌شود. اول یک نسخه پشتیبان خودکار از تنظیم‌های فعلی ذخیره می‌شود.\n\nادامه می‌دهی؟", "ورودی JSON")) return;
        var autoPath = Path.Combine(DatabaseService.GetAppDirectory(),
            $"hidden-settings-backup-auto-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        var (backupOk, backupMessage) = UiCustomizationService.ExportToFile(_db, autoPath);
        if (!backupOk)
        {
            StatusMessage = "❌ پشتیبان خودکار ساخته نشد: " + backupMessage;
            return;
        }
        var path = Path.Combine(DatabaseService.GetAppDirectory(), UiCustomizationService.BackupFileName);
        var (ok, message) = UiCustomizationService.ImportFromFile(_db, path);
        if (ok)
        {
            LoadUi();
            RefreshOverrides();
            ApplyUiToMainWindow();
            StatusMessage = message + $" (پشتیبان خودکار: {Path.GetFileName(autoPath)})";
        }
        else
        {
            StatusMessage = message;
        }
    }

    private void ResetUi()
    {
        // S-021: برگرداندن ظاهر به پیش‌فرض تاییدیه می‌خواهد.
        if (!ConfirmDanger("همه شخصی‌سازی ظاهر (نام‌ها، نمایش آیتم‌ها و نام نمایشی) به پیش‌فرض برمی‌گردد.\n\nادامه می‌دهی؟", "برگرداندن ظاهر به پیش‌فرض")) return;
        UiCustomizationService.Clear(_db);
        LoadUi();
        ApplyUiToMainWindow();
        StatusMessage = "✅ ظاهر به حالت پیش‌فرض برگشت.";
    }

    // ── Database tools ───────────────────────────────────────────────

    private void RefreshDbInfo()
    {
        try
        {
            var file = new FileInfo(DbPath);
            var size = file.Exists ? file.Length : 0;
            var counts = _db.GetRowCounts();
            DbInfo = $"مسیر: {DbPath}\nحجم فایل: {size / 1024.0:F0} کیلوبایت\n" +
                     $"پرونده‌ها: {counts.GetValueOrDefault("Cases")} | حق‌الزحمه: {counts.GetValueOrDefault("Fees")} | " +
                     $"مهندسین: {counts.GetValueOrDefault("Engineers")} | گزارش‌ها: {counts.GetValueOrDefault("Reports")} | " +
                     $"دریافتی‌ها: {counts.GetValueOrDefault("FeeReceipts")} | گزارش‌های تولیدشده: {counts.GetValueOrDefault("GeneratedReports")} | " +
                     $"اسنپ‌شات‌ها: {counts.GetValueOrDefault("Snapshots")}";
            RefreshRestorePoints();
        }
        catch (Exception ex)
        {
            DbInfo = "خواندن اطلاعات پایگاه داده نشد: " + ex.Message;
        }
    }

    private static string BackupsDir => Path.Combine(DatabaseService.GetAppDirectory(), "backups");

    private void RefreshRestorePoints()
    {
        RestorePoints.Clear();
        try
        {
            if (!Directory.Exists(BackupsDir)) return;
            foreach (var file in Directory.GetFiles(BackupsDir, "nezam-*.db").OrderByDescending(f => f))
            {
                var info = new FileInfo(file);
                RestorePoints.Add(new RestorePoint
                {
                    FileName = info.Name,
                    FullPath = file,
                    Info = $"{info.Name} — {info.LastWriteTime:yyyy/MM/dd HH:mm} — {info.Length / 1024.0:F0} کیلوبایت"
                });
            }
        }
        catch { }
    }

    private void CheckIntegrity()
    {
        try
        {
            var result = _db.CheckIntegrity();
            StatusMessage = result == "ok"
                ? "✅ پایگاه داده سالم است."
                : "❌ مشکل در پایگاه داده:\n" + result;
        }
        catch (Exception ex)
        {
            StatusMessage = "❌ بررسی نشد: " + ex.Message;
        }
    }

    private void Vacuum()
    {
        try
        {
            _db.Vacuum();
            RefreshDbInfo();
            StatusMessage = "✅ پایگاه داده فشرده شد.";
        }
        catch (Exception ex)
        {
            StatusMessage = "❌ فشرده‌سازی نشد: " + ex.Message;
        }
    }

    private void CreateRestorePoint()
    {
        try
        {
            Directory.CreateDirectory(BackupsDir);
            var name = $"nezam-{DateTime.Now:yyyyMMdd-HHmmss}.db";
            File.Copy(DbPath, Path.Combine(BackupsDir, name));
            RefreshRestorePoints();
            StatusMessage = $"✅ نقطه بازیابی ساخته شد: {name}";
        }
        catch (Exception ex)
        {
            StatusMessage = "❌ نقطه بازیابی ساخته نشد: " + ex.Message;
        }
    }

    private void RestoreSelectedPoint()
    {
        if (SelectedRestorePoint == null) return;
        var answer = MessageBox.Show(
            $"پایگاه‌داده فعلی با این نقطه بازیابی جایگزین می‌شود:\n{SelectedRestorePoint.FileName}\n\nبعد از آن برنامه بسته می‌شود و باید دوباره بازش کنی.\n\nادامه می‌دهی؟",
            "بازگردانی نقطه بازیابی",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        try
        {
            // Close the open connection first, or the file is locked.
            _db.Dispose();
            File.Copy(SelectedRestorePoint.FullPath, DbPath, overwrite: true);
            foreach (var suffix in new[] { "-journal", "-wal", "-shm" })
            {
                try { File.Delete(DbPath + suffix); } catch { }
            }
            MessageBox.Show("پایگاه داده برگردانده شد. برنامه را دوباره باز کن.",
                "بازگردانی انجام شد", MessageBoxButton.OK, MessageBoxImage.Information);
            System.Windows.Application.Current?.Shutdown();
        }
        catch (Exception ex)
        {
            StatusMessage = "❌ بازگردانی نشد: " + ex.Message;
        }
    }

    // ── Danger zone ──────────────────────────────────────────────────

    private static bool ConfirmDanger(string text, string title)
    {
        return MessageBox.Show(text, title, MessageBoxButton.YesNo, MessageBoxImage.Warning)
            == MessageBoxResult.Yes;
    }

    /// <summary>SEC-009: عملیات «منطقه خطر» اول رمز برنامه را دوباره می‌خواهد
    /// (با همان پنجره رمز ورود).</summary>
    private static bool AskAppPassword()
    {
        string? lastError = null;
        while (true)
        {
            var window = new Views.PasswordWindow(lastError) { Owner = Application.Current?.MainWindow };
            if (window.ShowDialog() != true) return false;
            if (VerifyAppPassword(window.Password)) return true;
            lastError = "رمز برنامه اشتباه است. دوباره تلاش کن.";
        }
    }

    private static bool VerifyAppPassword(string password)
    {
        try
        {
            var appDir = DatabaseService.GetAppDirectory();
            var credentialStore = new CredentialStore(Path.Combine(appDir, "data", "credentials.json"));
            var service = new DatabaseEncryptionService(credentialStore, Path.Combine(appDir, "data", "nezam_monitor.db"));
            // فقط برای بررسی باز می‌شود و همان لحظه بسته می‌شود.
            using var conn = service.Open(password);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void FactoryReset()
    {
        if (!AskAppPassword()) return;
        if (!ConfirmDanger("این عملیات تمام اطلاعات برنامه را پاک می‌کند.\nاطلاعات استخراج‌شده، تنظیمات، ویرایش‌ها، تاریخچه و اطلاعات ورود حذف خواهند شد.\n\nقابل بازگشت نیست مگر اینکه پشتیبان داشته باشید.\n\nآیا مطمین هستید؟", "پاکسازی کامل اطلاعات")) return;
        if (!ConfirmDanger("آیا واقعا می‌خواهید تمام اطلاعات را پاک کنید؟\nاین غیرقابل بازگشت است!", "تایید")) return;
        try
        {
            var cleared = _db.ClearAllData();
            var themeManager = new Services.ThemeManager(_db);
            themeManager.ResetToDefault();
            themeManager.ApplyTheme();
            // پیام با واقعیت می‌خواند و برنامه واقعا از اول باز می‌شود.
            MessageBox.Show(
                $"✅ پاکسازی کامل انجام شد — {cleared.Count} جدول خالی شد. برنامه دوباره باز می‌شود.",
                "پاکسازی کامل اطلاعات",
                MessageBoxButton.OK, MessageBoxImage.Information);
            App.Restart();
        }
        catch (Exception ex)
        {
            StatusMessage = "❌ خطا در پاکسازی: " + ex.Message;
        }
    }

    private void DeleteGeneratedReports()
    {
        if (!AskAppPassword()) return;
        if (!ConfirmDanger("همه گزارش‌های تولیدشده (لاگ تولید) حذف می‌شوند.\nفایل‌های Word و PDF روی دیسک سر جایشان می‌مانند.\n\nادامه می‌دهی؟", "حذف گزارش‌های تولیدشده")) return;
        try
        {
            var count = _db.DeleteAllGeneratedReports();
            StatusMessage = $"✅ {count} گزارش تولیدشده حذف شد.";
        }
        catch (Exception ex)
        {
            StatusMessage = "❌ حذف نشد: " + ex.Message;
        }
    }

    /// <summary>SEC-008 + S-024: «پاک کردن توکن» توکن را باطل نمی‌کرد؛
    /// حالا «خروج از سامانه» است: هم توکن و هم رمز ذخیره‌شده سامانه پاک می‌شود.</summary>
    private void Logout()
    {
        if (!AskAppPassword()) return;
        if (!ConfirmDanger("از سامانه خارج می‌شوی؛ توکن و رمز ذخیره‌شده سامانه پاک می‌شود. برای به‌روزرسانی بعدی باید دوباره وارد شوی.\n\nادامه می‌دهی؟", "خروج از سامانه")) return;
        try
        {
            _db.SaveSetting(SettingKeys.ApiToken, "");
            UpdateViewModel.DeleteSetting(_db, SettingKeys.Password);
            StatusMessage = "✅ از سامانه خارج شدی؛ برای به‌روزرسانی بعدی دوباره وارد شو.";
        }
        catch (Exception ex)
        {
            StatusMessage = "❌ خروج نشد: " + ex.Message;
        }
    }

    private void ResetSettings()
    {
        if (!AskAppPassword()) return;
        if (!ConfirmDanger("تنظیمات ظاهری (تم، حالت دکمه‌ها و شخصی‌سازی منو) به پیش‌فرض برمی‌گردد.\n\nادامه می‌دهی؟", "برگرداندن تنظیمات به پیش‌فرض")) return;
        try
        {
            // ARCH-003: دسترسی مستقیم به اتصال مشترک زیر قفل اتصال انجام می‌شود.
            lock (_db.SyncRoot)
            {
                using var cmd = _db.Connection.CreateCommand();
                cmd.CommandText = $"DELETE FROM Settings WHERE Key LIKE '{SettingKeys.ThemePrefix}%' OR Key='{SettingKeys.UiCustom}'";
                cmd.ExecuteNonQuery();
            }
            var themeManager = new Services.ThemeManager(_db);
            themeManager.ResetToDefault();
            themeManager.ApplyTheme();
            Services.ButtonStyleManager.ApplySaved(_db);
            ApplyUiToMainWindow();
            LoadUi();
            StatusMessage = "✅ تنظیمات به پیش‌فرض برگشت.";
        }
        catch (Exception ex)
        {
            StatusMessage = "❌ برگرداندن تنظیمات نشد: " + ex.Message;
        }
    }

    private void LockNow()
    {
        HiddenMenuSession.Unlocked = false;
        RequestClose?.Invoke();
    }

    private void ResetAllHidden()
    {
        if (!AskAppPassword()) return;
        if (!ConfirmDanger("همه تغییرهای منوی مخفی (ویرایش داده‌ها) و شخصی‌سازی ظاهر پاک می‌شود.\n\nادامه می‌دهی؟", "بازگردانی همه به پیش‌فرض")) return;
        try
        {
            foreach (var ov in _db.GetHiddenOverrides())
                _db.DeleteHiddenOverride(ov.Id);
            UiCustomizationService.Clear(_db);
            RefreshOverrides();
            LoadUi();
            ApplyUiToMainWindow();
            StatusMessage = "✅ همه تنظیم‌های مخفی به پیش‌فرض برگشت.";
        }
        catch (Exception ex)
        {
            StatusMessage = "❌ برگرداندن نشد: " + ex.Message;
        }
    }
}
