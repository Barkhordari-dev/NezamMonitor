using System.Globalization;
using System.Text;

namespace NezamMonitorPro.Core;

/// <summary>
/// Persian Solar Hijri date utility using .NET's built-in PersianCalendar.
/// </summary>
public static class PersianDateHelper
{
    private static readonly PersianCalendar _pc = new();

    /// <summary>
    /// Convert Gregorian DateTime to Persian Solar Hijri date string (yyyy/MM/dd).
    /// </summary>
    public static string ToPersianDate(DateTime date)
    {
        int y = _pc.GetYear(date);
        int m = _pc.GetMonth(date);
        int d = _pc.GetDayOfMonth(date);
        return $"{y:0000}/{m:00}/{d:00}";
    }

    /// <summary>
    /// Convert to Persian date with Persian digits (۱۴۰۵/۰۶/۰۵).
    /// </summary>
    public static string ToPersianDateDigits(DateTime date)
    {
        return TextNormalizer.ToPersianDigits(ToPersianDate(date));
    }

    /// <summary>
    /// Convert to filename-safe format: ۱۴۰۵-۰۶-۰۵ (no slash, Persian digits).
    /// </summary>
    public static string ToPersianDateFile(DateTime date)
    {
        return TextNormalizer.ToPersianDigits(ToPersianDate(date).Replace("/", "-"));
    }

    /// <summary>تاریخ+ساعت میلادی (DateTime) به شمسی yyyy/MM/dd HH:mm</summary>
    public static string ToShamsiDateTime(DateTime dt)
        => $"{ToPersianDate(dt)} {dt:HH:mm}";

    /// <summary>
    /// رشته تاریخ میلادی "yyyy/MM/dd HH:mm" (یا با ثانیه/خط‌تیره) را به شمسی "yyyy/MM/dd HH:mm" تبدیل می‌کند.
    /// اگر پارس نشد، خود رشته برگردانده می‌شود.
    /// توجه: تاریخ‌هایی که قبلا به‌اشتباه با تقویم شمسی ذخیره شده‌اند (سال ۱۳۰۰ تا ۱۴۹۹،
    /// چون App فرهنگ fa-IR را پیش‌فرض می‌کند و ToString بدون InvariantCulture شمسی می‌دهد)
    /// دوباره تبدیل نمی‌شوند؛ همان رشته برگردانده می‌شود تا به سال ۰۷۸۴ تبدیل نشود.
    /// </summary>
    public static string GregoryStringToShamsi(string? gregorianDateTime)
    {
        if (string.IsNullOrWhiteSpace(gregorianDateTime)) return "";
        var s = gregorianDateTime.Trim();
        // فرمت‌های رایج ذخیره‌شده: yyyy/MM/dd HH:mm , yyyy/MM/dd HH:mm:ss , yyyy-MM-dd ...
        string[] fmts = { "yyyy/MM/dd HH:mm:ss", "yyyy/MM/dd HH:mm", "yyyy/MM/dd",
                          "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd",
                          "yyyy/MM/ddTHH:mm:ss", "O", "o" };
        if (DateTime.TryParseExact(s, fmts, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt)
            || DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out dt))
        {
            // سال ۱۳۰۰ تا ۱۴۹۹ یعنی تاریخ در واقع شمسی است، نه میلادی ۱۴۰۵.
            if (dt.Year >= 1300 && dt.Year <= 1499)
                return s;
            return ToShamsiDateTime(dt);
        }
        return s;
    }

    /// <summary>
    /// رشته تاریخ را به میلادی "yyyy/MM/dd HH:mm" نرمال می‌کند.
    /// اگر رشته در واقع شمسی ذخیره شده باشد (سال ۱۳۰۰ تا ۱۴۹۹)، آن را به میلادی تبدیل می‌کند؛
    /// در غیر این صورت رشته بدون تغییر برگردانده می‌شود.
    /// </summary>
    public static string NormalizeToGregorian(string? dateTimeString)
    {
        if (string.IsNullOrWhiteSpace(dateTimeString)) return "";
        var s = dateTimeString.Trim();
        string[] fmts = { "yyyy/MM/dd HH:mm:ss", "yyyy/MM/dd HH:mm", "yyyy/MM/dd",
                          "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd" };
        if (!DateTime.TryParseExact(s, fmts, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt)
            && !DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out dt))
            return s;
        if (dt.Year < 1300 || dt.Year > 1499) return s;
        try
        {
            var gregorian = _pc.ToDateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, 0);
            return gregorian.ToString("yyyy/MM/dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        }
        catch
        {
            return s;
        }
    }
}
