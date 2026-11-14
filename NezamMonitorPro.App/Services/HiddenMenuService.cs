using System.Text.RegularExpressions;
using NezamMonitorPro.Core;
using NezamMonitorPro.Core.Data;

namespace NezamMonitorPro.App.Services;

/// <summary>Session-only unlock flag for the hidden menu. Process memory
/// ONLY — never persisted. When the app closes it is false again and the
/// menu locks.</summary>
public static class HiddenMenuSession
{
    public static bool Unlocked { get; set; }
}

public enum HiddenPasswordResult { OK, WRONG, NO_NETWORK, NO_FOLDER_ID, LOCKED }

/// <summary>
/// Hidden-menu password: the name of the FIRST folder inside a Drive
/// folder (same trick as the Android app — the outer folder's own name is
/// NOT the password). The folder id lives in the encrypted DB setting
/// hidden_menu_folder_id (entered once at first setup); the password
/// itself is NEVER written anywhere — it is fetched from Drive at unlock
/// time, and only the last-known name is cached for offline use.
///
/// SEC-009: این «رمز» امنیت واقعی نیست (اسم یک پوشه عمومی است)؛ برای
/// همین حدس‌زدن با تکرار سریع محدود شده: تاخیر تصاعدی بعد از تلاش
/// ناموفق و قفل موقت بعد از ۵ تلاش اشتباه پشت سر هم.
/// </summary>
public static class HiddenMenuService
{
    // SEC-009: شمارنده تلاش‌های ناموفق و قفل موقت — فقط در حافظه است
    // (با بستن برنامه صفر می‌شود).
    private static int _failedAttempts;
    private static DateTime _lockoutUntilUtc = DateTime.MinValue;

    /// <summary>ثانیه‌های باقی‌مانده قفل موقت (null یعنی قفل نیست).</summary>
    public static int? LockoutRemainingSeconds
    {
        get
        {
            var remaining = _lockoutUntilUtc - DateTime.UtcNow;
            return remaining > TimeSpan.Zero ? (int)Math.Ceiling(remaining.TotalSeconds) : null;
        }
    }

    public static string? GetFolderId(NezamDatabase db)
    {
        try
        {
            var id = db.GetSetting(SettingKeys.HiddenMenuFolderId);
            return string.IsNullOrWhiteSpace(id) ? null : id.Trim();
        }
        catch
        {
            // پایگاه‌داده در دسترس نیست؛ راه‌اندازی دوباره آن را نشان می‌دهد.
            return null;
        }
    }

    /// <summary>Accepts a bare folder id or a full Drive folder URL
    /// (.../drive/folders/&lt;id&gt;...); returns the id or null.</summary>
    public static string? ParseFolderId(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var text = input.Trim();
        var m = Regex.Match(text, @"drive/folders/([A-Za-z0-9_-]+)");
        if (m.Success) return m.Groups[1].Value;
        if (Regex.IsMatch(text, @"^[A-Za-z0-9_-]{10,}$")) return text;
        return null;
    }

    /// <summary>Verifies a folder id by asking Drive for its page, then
    /// saves it — and reads it back to confirm the save really landed.
    /// Returns false when the id is bad, unreachable, or the save did
    /// not persist. Never throws.</summary>
    public static async Task<bool> SaveFolderIdAsync(NezamDatabase db, string folderId)
    {
        try
        {
            if (!await DriveListingService.CheckFolderReachableAsync(folderId).ConfigureAwait(false))
                return false;
            db.SaveSetting(SettingKeys.HiddenMenuFolderId, folderId);
            // تایید ذخیره: دوباره بخوان؛ اگر ننشسته بود، یعنی ذخیره نشده.
            return GetFolderId(db) == folderId;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Checks the entered password against the first subfolder's
    /// name: fetch it live (and cache it on success); on a network failure
    /// fall back to the last cached name — of any age, because folder
    /// names do not rotate daily (the cache is refreshed whenever the
    /// fetch works). With no cached name either, report NO_NETWORK. The
    /// comparison normalizes digits and letters first
    /// (NormalizeForCompare), so a name typed with a Persian keyboard
    /// still matches. Never throws: any unexpected error falls back to
    /// the cache, then NO_NETWORK. SEC-009: after failed attempts the
    /// check is delayed (5, 15, 60 seconds) and after 5 consecutive
    /// wrong attempts the menu locks for 5 minutes.</summary>
    public static async Task<HiddenPasswordResult> CheckPasswordAsync(NezamDatabase db, string entered)
    {
        var folderId = GetFolderId(db);
        if (folderId == null) return HiddenPasswordResult.NO_FOLDER_ID;

        // SEC-009: اگر قفل موقت فعال است، اصلا بررسی نمی‌کنیم.
        if (LockoutRemainingSeconds != null) return HiddenPasswordResult.LOCKED;

        // SEC-009: تاخیر تصاعدی بعد از تلاش‌های ناموفق (تلاش ۳ به بعد: ۵، ۱۵، ۶۰ ثانیه).
        if (_failedAttempts >= 2)
        {
            var delaySeconds = _failedAttempts == 2 ? 5 : _failedAttempts == 3 ? 15 : 60;
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds)).ConfigureAwait(false);
            if (LockoutRemainingSeconds != null) return HiddenPasswordResult.LOCKED;
        }

        try
        {
            var entries = await DriveListingService.FetchListingAsync(folderId).ConfigureAwait(false);
            var firstFolder = entries.FirstOrDefault(e => e.IsFolder);
            if (firstFolder != null)
            {
                // نام تازه را کش می‌کنیم و زمانش را هم می‌نویسیم (فقط برای
                // اطلاع؛ انقضا ندارد — اسم پوشه هر روز عوض نمی‌شود).
                db.SaveSetting(SettingKeys.HiddenMenuCachedName, firstFolder.Name);
                db.SaveSetting(SettingKeys.HiddenMenuCachedAt, DateTime.UtcNow.Ticks.ToString());
                return Check(entered, firstFolder.Name);
            }
        }
        catch
        {
            // هر خطای غیرمنتظره (مثلاً پایگاه‌داده) → سراغ کش می‌رویم.
        }

        // خواندن زنده نشد (اینترنت نیست یا صفحه درایو خوانده نشد)؛
        // اسم کش‌شده — با هر سنی — هنوز معتبر است.
        var cached = TryGetCachedName(db);
        if (cached == null) return HiddenPasswordResult.NO_NETWORK;
        return Check(entered, cached);
    }

    /// <summary>Reads the cached password name, if any. Never throws.</summary>
    private static string? TryGetCachedName(NezamDatabase db)
    {
        try
        {
            var cached = db.GetSetting(SettingKeys.HiddenMenuCachedName);
            return string.IsNullOrWhiteSpace(cached) ? null : cached;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>مقایسه رمز: ارقام فارسی/عربی به لاتین (۰۹۰۲۲۲۳۳۳۵۷ ←
    /// 09022233357)، ي/ك عربی به ی/ک، حذف فاصله‌های سر و ته و فشرده‌سازی
    /// فاصله‌های میانی. نامی که با صفحه‌کلید فارسی تایپ شده همان رمز
    /// حساب می‌شود. روی هر دو طرف مقایسه اعمال می‌شود (ورودی و نام
    /// گرفته‌شده از درایو/کش).</summary>
    public static string NormalizeForCompare(string? input)
        => DigitNormalizer.ToAsciiDigits(DigitNormalizer.Normalize(input));

    private static HiddenPasswordResult Check(string entered, string expected)
    {
        if (NormalizeForCompare(entered) == NormalizeForCompare(expected))
        {
            _failedAttempts = 0;
            return HiddenPasswordResult.OK;
        }
        _failedAttempts++;
        if (_failedAttempts >= 5)
            _lockoutUntilUtc = DateTime.UtcNow.AddMinutes(5);
        return HiddenPasswordResult.WRONG;
    }
}
