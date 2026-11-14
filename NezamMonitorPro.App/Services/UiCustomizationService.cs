using System.IO;
using System.Text.Json;
using NezamMonitorPro.Core.Data;

namespace NezamMonitorPro.App.Services;

/// <summary>One sidebar item's hidden-menu customisation.</summary>
public sealed class NavItemCustom
{
    public string Title { get; set; } = "";
    public bool Visible { get; set; } = true;
}

/// <summary>
/// Hidden-menu UI customisation (mirrors Android's UiCustomization):
/// per-sidebar-item title + show/hide, a display-name override for the
/// dashboard welcome, and hiding the «نسخه پرو» badge. Stored as JSON in
/// the ui_custom setting. Nav keys are the MainWindow RadioButton Tags.
/// </summary>
public sealed class UiCustomization
{
    public Dictionary<string, NavItemCustom> NavItems { get; set; } = new();
    public string DisplayName { get; set; } = "";
    public bool HideProBadge { get; set; }
}

public static class UiCustomizationService
{
    public const string SettingKey = SettingKeys.UiCustom;
    public const string BackupFileName = "hidden-settings-backup.json";

    /// <summary>(nav Tag, default title) in sidebar order.</summary>
    public static readonly (string Tag, string Title)[] NavDefaults =
    {
        ("dashboard", "داشبورد"),
        ("update", "به‌روزرسانی"),
        ("cases", "پرونده‌ها"),
        ("generator", "تولید گزارش"),
        ("reports", "گزارش‌ها"),
        ("excel", "خروجی اکسل"),
        ("androidexport", "خروجی اندروید"),
        ("history", "تاریخچه و بایگانی"),
        ("changes", "تغییرات"),
        ("engineers", "مهندسین"),
        ("fees", "حق‌الزحمه"),
        ("followup", "پیگیری"),
        ("settings", "تنظیمات")
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static UiCustomization Load(NezamDatabase db)
    {
        var custom = new UiCustomization();
        try
        {
            var json = db.GetSetting(SettingKey);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var parsed = JsonSerializer.Deserialize<UiCustomization>(json, JsonOptions);
                if (parsed != null) custom = parsed;
            }
        }
        catch { }
        // Fill in anything the saved JSON does not mention.
        foreach (var (tag, title) in NavDefaults)
        {
            if (!custom.NavItems.TryGetValue(tag, out var item) || item == null)
                custom.NavItems[tag] = new NavItemCustom { Title = title, Visible = true };
            else if (string.IsNullOrWhiteSpace(item.Title))
                item.Title = title;
        }
        return custom;
    }

    public static void Save(NezamDatabase db, UiCustomization custom)
    {
        try
        {
            db.SaveSetting(SettingKey, JsonSerializer.Serialize(custom, JsonOptions));
        }
        catch { }
    }

    public static void Clear(NezamDatabase db) => db.SaveSetting(SettingKey, "");

    public static string GetDisplayNameOverride(NezamDatabase db)
    {
        try { return Load(db).DisplayName?.Trim() ?? ""; } catch { return ""; }
    }

    private sealed class HiddenBackup
    {
        public int Version { get; set; } = 1;
        public UiCustomization? UiCustom { get; set; }
        public List<HiddenOverride> Overrides { get; set; } = new();
    }

    /// <summary>Writes the UI customisation + permanent overrides to
    /// &lt;exe&gt;/hidden-settings-backup.json. Returns (ok, message).</summary>
    public static (bool Ok, string Message) ExportToFile(NezamDatabase db, string path)
    {
        try
        {
            var backup = new HiddenBackup
            {
                UiCustom = Load(db),
                Overrides = db.GetHiddenOverrides().Where(o => o.Mode == "perm").ToList()
            };
            File.WriteAllText(path, JsonSerializer.Serialize(backup, JsonOptions));
            return (true, $"✅ پشتیبان در {path} ذخیره شد ({backup.Overrides.Count} تغییر دایمی).");
        }
        catch (Exception ex)
        {
            return (false, "❌ ذخیره پشتیبان نشد: " + ex.Message);
        }
    }

    /// <summary>Restores the UI customisation + permanent overrides from a
    /// backup file (permanent overrides are replaced wholesale). Returns
    /// (ok, message).</summary>
    public static (bool Ok, string Message) ImportFromFile(NezamDatabase db, string path)
    {
        try
        {
            if (!File.Exists(path)) return (false, "❌ فایل پشتیبان پیدا نشد.");
            var backup = JsonSerializer.Deserialize<HiddenBackup>(File.ReadAllText(path), JsonOptions);
            if (backup == null) return (false, "❌ فایل پشتیبان خوانده نشد.");
            if (backup.UiCustom != null) Save(db, backup.UiCustom);
            foreach (var old in db.GetHiddenOverrides().Where(o => o.Mode == "perm"))
                db.DeleteHiddenOverride(old.Id);
            foreach (var ov in backup.Overrides)
            {
                ov.Mode = "perm";
                ov.UpdatedAt = HiddenOverrideApplier.NowJalali();
                db.SaveHiddenOverride(ov);
            }
            return (true, $"✅ پشتیبان برگردانده شد ({backup.Overrides.Count} تغییر دایمی).");
        }
        catch (Exception ex)
        {
            return (false, "❌ برگرداندن پشتیبان نشد: " + ex.Message);
        }
    }
}
