using System.Globalization;
using System.Text.RegularExpressions;

namespace NezamMonitorPro.Core.Reports;

/// <summary>
/// تشخیص هوشمند «مرحله» و «رشته» از نام فایل قالب Word.
/// مثال‌هایی که باید یکی تشخیص داده شوند:
///   «مکانیک مرحله اول.docx»  ≡  «1.docx» (داخل پوشهٔ مکانیک)  ≡  «مرحله ۱ مکانیک.docx»
///   «معماری-مرحله-2.docx»    ≡  «2.docx» (داخل پوشهٔ معماری)
/// </summary>
public static class TemplateNameParser
{
    /// <summary>ترتیب اعداد ترتیبی فارسی برای مراحل ۱ تا ۱۲.</summary>
    private static readonly string[] PersianOrdinals =
    {
        "اول", "دوم", "سوم", "چهارم", "پنجم", "ششم",
        "هفتم", "هشتم", "نهم", "دهم", "یازدهم", "دوازدهم"
    };

    /// <summary>نام‌های جایگزین رشته‌ها (برای تطبیق مقاوم به املای متفاوت).</summary>
    private static readonly (string Canonical, string[] Aliases)[] Disciplines =
    {
        ("مکانیک",      new[] { "مکانیک", "مكانيك", "mechanical", "mechanic", "mech" }),
        ("معماری",      new[] { "معماری", "معمارى", "معماري", "architecture", "arch" }),
        ("عمران",       new[] { "عمران", "civil" }),
        ("برق",         new[] { "برق", "electrical", "electric" }),
        ("نقشه‌برداری", new[] { "نقشه برداری", "نقشه‌برداری", "نقشهبرداری", "survey" }),
        ("تاسیسات",     new[] { "تاسیسات", "تأسیسات", "تاسيسات", "installation", "hvac" }),
        ("شهرسازی",     new[] { "شهرسازی", "شهرسازي", "urban" }),
        ("زلزله",       new[] { "زلزله", "earthquake", "seismic" })
    };

    /// <summary>نتیجهٔ تشخیص یک فایل قالب.</summary>
    public sealed record ParsedTemplate(string Path, string FileName, int Stage, string Discipline);

    /// <summary>نام فایل/پوشه را برای مقایسه نرمال می‌کند (اعداد فارسی → لاتین، حذف نیم‌فاصله).</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";

        var chars = new char[value.Length];
        for (int i = 0; i < value.Length; i++)
        {
            var c = value[i];
            chars[i] = c switch
            {
                >= '\u06F0' and <= '\u06F9' => (char)('0' + (c - '\u06F0')), // ۰-۹
                >= '\u0660' and <= '\u0669' => (char)('0' + (c - '\u0660')), // ٠-٩
                '\u064A' => '\u06CC', // ي → ی
                '\u0643' => '\u06A9', // ك → ک
                '\u200C' => ' ',      // نیم‌فاصله
                '\u200E' or '\u200F' => ' ',
                '-' or '_' or '.' => ' ',
                _ => c
            };
        }

        var result = new string(chars);
        return Regex.Replace(result, @"\s+", " ").Trim();
    }

    /// <summary>تشخیص شمارهٔ مرحله (۰ = ناشناخته).</summary>
    public static int DetectStage(string? nameOrPath)
    {
        var text = Normalize(nameOrPath);
        if (text.Length == 0) return 0;

        // ۱) اعداد ترتیبی فارسی: «مرحله اول»، «اول»، «سوم» ...
        for (int i = 0; i < PersianOrdinals.Length; i++)
        {
            if (Regex.IsMatch(text, $@"(^|\s){PersianOrdinals[i]}(\s|$)"))
                return i + 1;
        }

        // ۲) صریح: مرحله ۳ / مرحله3 / stage 2 / step 4
        int stage;
        var m = Regex.Match(text, @"(?:مرحله|stage|step)\s*(\d{1,2})", RegexOptions.IgnoreCase);
        if (m.Success && TryStage(m.Groups[1].Value, out stage)) return stage;

        // ۳) نام فایل صرفاً عددی: «1.docx» یا «۳.docx»
        m = Regex.Match(text, @"^(\d{1,2})$");
        if (m.Success && TryStage(m.Groups[1].Value, out stage)) return stage;

        // ۴) عدد در انتهای نام: «مکانیک 1»، «معماری مرحله 2»
        m = Regex.Match(text, @"(\d{1,2})\s*$");
        if (m.Success && TryStage(m.Groups[1].Value, out stage)) return stage;

        // ۵) عدد در ابتدای نام: «1 مکانیک»
        m = Regex.Match(text, @"^(\d{1,2})(\s|$)");
        if (m.Success && TryStage(m.Groups[1].Value, out stage)) return stage;

        return 0;
    }

    private static bool TryStage(string digits, out int stage)
    {
        stage = 0;
        if (!int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) return false;
        if (value < 1 || value > 20) return false;
        stage = value;
        return true;
    }

    /// <summary>تشخیص رشتهٔ مهندسی (خالی = عمومی).</summary>
    public static string DetectDiscipline(string? nameOrPath)
    {
        var text = Normalize(nameOrPath);
        if (text.Length == 0) return "";

        foreach (var (canonical, aliases) in Disciplines)
        {
            foreach (var alias in aliases)
            {
                if (text.Contains(alias, StringComparison.OrdinalIgnoreCase))
                    return canonical;
            }
        }

        return "";
    }

    /// <summary>
    /// یک مسیر قالب را تحلیل می‌کند. اگر نام فایل مرحله نداشت، از نام پوشهٔ والد هم استفاده می‌شود
    /// (مثلاً پوشهٔ «مکانیک/1.docx» → مکانیک، مرحلهٔ ۱).
    /// </summary>
    public static ParsedTemplate Parse(string path)
    {
        var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
        var parent = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path) ?? "");

        var stage = DetectStage(fileName);
        if (stage == 0) stage = DetectStage(parent);

        var discipline = DetectDiscipline(fileName);
        if (discipline.Length == 0) discipline = DetectDiscipline(parent);

        return new ParsedTemplate(path, fileName, stage, discipline);
    }
}
