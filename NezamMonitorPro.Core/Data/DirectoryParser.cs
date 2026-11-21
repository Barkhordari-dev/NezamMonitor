using System.Text.RegularExpressions;

namespace NezamMonitorPro.Core.Data;

// پورت خالص DirectoryParser اندروید (v3.11.4 تا v3.11.6): فقط توابعِ
// خالصِ نرمال‌سازی، تطبیق نام دفتر/مهندس و کمک‌های نمایشی. هیچ شبکه‌ای
// اینجا نیست؛ تنها استثنا ParseEngineerPhotoFromHtml است که یک رشتهٔ
// HTML آماده را تجزیه می‌کند (بدون jsoup/HtmlAgilityPack — با regex
// کوچک). هیچ رشتهٔ تحصیلی/شغلی‌ای هاردکد نشده است.
public static class DirectoryParser
{
    /// <summary>آدرس پایهٔ سایت دفترچهٔ دفاتر و مهندسین یزد.</summary>
    public const string BaseUrl = "http://service.yazdnezam.ir:8033";

    /// <summary>نام فایل عکس جایگزین سایت به‌معنی «عکس ندارد».</summary>
    public const string PlaceholderPhoto = "images.png";

    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex DigitRunRegex = new("[0-9۰-۹٠-٩]+", RegexOptions.Compiled);
    private static readonly Regex BareNumberRegex = new("^[0-9۰-۹٠-٩]+$", RegexOptions.Compiled);
    private static readonly Regex ImgTagRegex = new("<img\\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // ---- نرمال‌سازی (همان قواعد جست‌وجوی پرونده‌ها) ----

    /// <summary>حروف عربی به فارسی: ي→ی، ك→ک.</summary>
    public static string ToPersianChars(string? input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return input.Replace('\u064A', '\u06CC').Replace('\u0643', '\u06A9');
    }

    /// <summary>ارقام فارسی/عربی به لاتین.</summary>
    public static string NormalizeDigits(string? input) => DigitNormalizer.ToAsciiDigits(input);

    /// <summary>
    /// کلید مقایسه/جست‌وجو: خط فارسی، ارقام لاتین، بدون نیم‌فاصله/کَشیدگی،
    /// با تاشدنِ گونه‌های حروف عربی. روی هر دو طرفِ هر مقایسهٔ نام
    /// (نام دفتر، نام مهندس، عبارت جست‌وجو) اعمال می‌شود. از
    /// TextNormalizer.NormalizeForCompare شروع می‌کند (که خودش ي/ك و
    /// ارقام لاتین و مرتب‌سازی فاصله‌ها را انجام می‌دهد) و تاهای
    /// اندروید را اضافه می‌کند؛ خود DigitNormalizer/TextNormalizer
    /// دست نخورده‌اند.
    /// </summary>
    public static string NormalizeForCompare(string? input)
    {
        var s = TextNormalizer.NormalizeForCompare(input);
        if (s.Length == 0) return "";
        return s
            .Replace('\u0629', '\u0647') // ة→ه
            .Replace('\u0624', '\u0648') // ؤ→و
            .Replace('\u0623', '\u0627') // أ→ا
            .Replace('\u0625', '\u0627') // إ→ا
            .Replace('\u0622', '\u0627') // آ→ا
            .Replace("\u0640", "") // کَشیدگی (تطویل)
            .Replace("\u200C", "") // نیم‌فاصله
            .Replace("\u200D", ""); // اتصال‌دهندهٔ بدون عرض
    }

    /// <summary>متن نمایشی: خط فارسی، فاصله‌های پشت‌سرهم یکی‌شده.</summary>
    public static string CleanDisplay(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        return WhitespaceRegex.Replace(ToPersianChars(input), " ").Trim();
    }

    /// <summary>متن مجوز/تاریخ/تلفن: خط فارسی، ارقام لاتین.</summary>
    public static string CleanCode(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        return WhitespaceRegex.Replace(NormalizeDigits(ToPersianChars(input)), " ").Trim();
    }

    // ---- پرس‌وجوی نام دفتر و تطبیقِ دقیق (v3.11.0 / v3.11.4) ----

    /// <summary>
    /// نام کانونی دفتر از متنِ شلختهٔ پرونده. عددِ تنها («۱۹۴») یعنی
    /// دفتر N؛ متنی بدون عدد (نام شرکت) همان‌طور که هست می‌ماند؛ وگرنه
    /// اولین رشتهٔ عدد یعنی دفتر N. بررسی نشانگر («دفتر»/«شماره») جلوی
    /// این را می‌گیرد که نام شرکتی که اتفاقی عدد دارد به «دفتر N»
    /// بازنویسی شود.
    /// </summary>
    public static string OfficeQueryName(string? raw)
    {
        var text = (raw ?? "").Trim();
        if (text.Length == 0) return "";
        // عدد تنها («194»، «۱۹۴») یعنی دفتر N.
        if (BareNumberRegex.IsMatch(text)) return "دفتر " + NormalizeDigits(text);
        var key = NormalizeForCompare(text);
        if (!key.Contains("دفتر") && !key.Contains("شماره")) return text;
        var digits = DigitRunRegex.Match(text).Value;
        if (digits.Length == 0) return text;
        return "دفتر " + NormalizeDigits(digits);
    }

    /// <summary>اولین رشتهٔ عددِ یک مقدار، به ارقام لاتین، یا null.</summary>
    public static string? ExtractNumber(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        var m = DigitRunRegex.Match(raw);
        return m.Success ? NormalizeDigits(m.Value) : null;
    }

    /// <summary>
    /// حذف پیشوند «مهندس» از ابتدای نام («مهندس X»، «مهندس‌X»). فقط
    /// وقتی بعد از عنوان، فاصله یا نیم‌فاصله بیاید — نامی که فقط با
    /// همان حروف شروع می‌شود («مهندسان مشاور…») دست نمی‌خورد.
    /// </summary>
    public static string StripEngineerTitle(string? raw)
    {
        var trimmed = (raw ?? "").Trim();
        string stripped;
        if (trimmed.StartsWith("مهندس ", StringComparison.Ordinal))
            stripped = trimmed.Substring("مهندس ".Length).Trim();
        else if (trimmed.StartsWith("مهندس\u200C", StringComparison.Ordinal))
            stripped = trimmed.Substring("مهندس\u200C".Length).Trim();
        else
            stripped = trimmed;
        return NormalizeForCompare(stripped);
    }

    /// <summary>
    /// تطبیق مقدار کلیک‌شدهٔ دفتر با ردیف ذخیره‌شده، یا null.
    /// ۱) تطبیق دقیق نام («دفتر 194»). ۲) شکلِ پرس‌وجو («شماره 194 يزد»
    /// یا «۱۹۴» تنها ← «دفتر 194»). ۳) تطبیق عددی — فقط برای عددِ تنها
    /// یا مقداری که نشانگر «دفتر»/«شماره» دارد، تا نام شرکتی که اتفاقی
    /// عدد دارد هرگز به دفتر نرسد. هیچ جایگزینیِ نزدیک انجام نمی‌شود:
    /// دفترهای شبح (78/89/104…) «یافت نشد» می‌مانند.
    /// </summary>
    public static DirectoryOffice? ResolveOffice(string? raw, IReadOnlyList<DirectoryOffice> offices)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var key = NormalizeForCompare(raw);
        foreach (var office in offices)
        {
            if (NormalizeForCompare(office.Name) == key) return office;
        }
        var queryKey = NormalizeForCompare(OfficeQueryName(raw));
        if (queryKey.Length != 0 && queryKey != key)
        {
            foreach (var office in offices)
            {
                if (NormalizeForCompare(office.Name) == queryKey) return office;
            }
        }
        var isBareNumber = Regex.IsMatch(key, "^[0-9]+$");
        var hasMarker = key.Contains("دفتر") || key.Contains("شماره");
        if (isBareNumber || hasMarker)
        {
            var number = ExtractNumber(raw);
            if (number == null) return null;
            foreach (var office in offices)
            {
                if (ExtractNumber(office.Name) == number) return office;
            }
        }
        return null;
    }

    /// <summary>
    /// تطبیق نام کلیک‌شدهٔ مهندس با ردیف ذخیره‌شده، یا null. هر دو شکل
    /// (همان‌طور که هست، و بدون پیشوند «مهندس») را می‌آزماید؛ هر شکل
    /// اول با «نام + نام خانوادگی» کامل، بعد فقط نام، بعد فقط نام
    /// خانوادگی مطابقت می‌کند.
    /// </summary>
    public static DirectoryEngineer? ResolveEngineer(string? raw, IReadOnlyList<DirectoryEngineer> engineers)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var keys = new[] { NormalizeForCompare(raw), StripEngineerTitle(raw) }
            .Where(k => k.Length != 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        foreach (var key in keys)
        {
            foreach (var eng in engineers)
            {
                if (NormalizeForCompare(eng.Name + " " + eng.Family) == key) return eng;
            }
            foreach (var eng in engineers)
            {
                if (NormalizeForCompare(eng.Name) == key) return eng;
            }
            foreach (var eng in engineers)
            {
                if (NormalizeForCompare(eng.Family) == key) return eng;
            }
        }
        return null;
    }

    // ---- کمک‌های نمایشی یکدست (v3.11.6) ----

    /// <summary>نشانگرهای «چیزی ثبت نشده» در سایت: خالی، یا «_» / «-».</summary>
    public static bool IsMissingDirectoryValue(string? value)
    {
        var v = (value ?? "").Trim();
        return v.Length == 0 || v == "_" || v == "-";
    }

    /// <summary>شکل نمایشی یک مقدار: خودش، یا «ثبت نشده» وقتی خالی است.</summary>
    public static string DirectoryDisplayValue(string? value)
        => IsMissingDirectoryValue(value) ? "ثبت نشده" : value!.Trim();

    /// <summary>
    /// تاریخ جلالیِ قابل‌قبول یعنی yyyy/mm/dd با سالِ ≥ 1300 و ماه/روز
    /// واقعی. بقیه («1278/10/11»، «_»، خالی) تاریخ نیستند.
    /// </summary>
    public static bool IsPlausibleJalaliDate(string? value)
    {
        var v = (value ?? "").Trim();
        if (IsMissingDirectoryValue(v)) return false;
        var parts = v.Split('/');
        if (parts.Length != 3) return false;
        if (!int.TryParse(parts[0].Trim(), out var year)) return false;
        if (!int.TryParse(parts[1].Trim(), out var month)) return false;
        if (!int.TryParse(parts[2].Trim(), out var day)) return false;
        return year >= 1300 && month >= 1 && month <= 12 && day >= 1 && day <= 31;
    }

    /// <summary>تاریخِ اخذ یک پایهٔ پروانه، یا null وقتی هرگز اخذ نشده (یا تاریخِ ذخیره‌شده خراب است).</summary>
    public static string? LicenceGradeDate(string? value)
        => IsPlausibleJalaliDate(value) ? value!.Trim() : null;

    /// <summary>ارقام لاتین به فارسی، برای نمایش.</summary>
    public static string ToPersianDigits(string? value) => DigitNormalizer.ToPersianDigits(value);

    /// <summary>
    /// بازهٔ تاریخ تحصیل. سایت برای تاریخِ نامعلوم «1300/01/01» ذخیره
    /// می‌کند — آن گم‌شده است، نه یک تاریخِ واقعیِ سال 1300. طرفِ
    /// گم‌شده حذف می‌شود، نه جعل: «X تا Y» / «تا Y» / «از X» /
    /// «تاریخ ثبت نشده».
    /// </summary>
    public static string EducationDateText(string? start, string? end)
    {
        var s = EducationDateOrNull(start);
        var e = EducationDateOrNull(end);
        if (s != null && e != null) return s + " تا " + e;
        if (s == null && e != null) return "تا " + e;
        if (s != null && e == null) return "از " + s;
        return "تاریخ ثبت نشده";
    }

    private static string? EducationDateOrNull(string? value)
    {
        var v = (value ?? "").Trim();
        if (v == "1300/01/01" || !IsPlausibleJalaliDate(v)) return null;
        return v;
    }

    // ---- عکس پروفایل مهندس از HTML صفحهٔ جزئیات ----

    /// <summary>
    /// عکس پروفایل را از HTML صفحهٔ جزئیات مهندس بیرون می‌کشد: اولین
    /// ‎&lt;img&gt;‎ای که کلاسش ‎img-fluid‎ دارد (src و class به هر
    /// ترتیبی می‌توانند بیایند). ‎""‎ برمی‌گرداند وقتی چنین عکسی نیست
    /// یا src همان جایگزینِ «عکس ندارد» (images.png) باشد. src نسبی
    /// با BaseUrl کامل می‌شود.
    /// </summary>
    public static string ParseEngineerPhotoFromHtml(string? html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        foreach (Match tag in ImgTagRegex.Matches(html))
        {
            var classAttr = GetHtmlAttribute(tag.Value, "class");
            if (classAttr == null) continue;
            var hasImgFluid = classAttr
                .Split((char[])null!, StringSplitOptions.RemoveEmptyEntries)
                .Any(c => string.Equals(c, "img-fluid", StringComparison.OrdinalIgnoreCase));
            if (!hasImgFluid) continue;
            var src = GetHtmlAttribute(tag.Value, "src") ?? "";
            if (src.Length == 0 || src.Contains(PlaceholderPhoto, StringComparison.Ordinal)) return "";
            return src.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? src : BaseUrl + src;
        }
        return "";
    }

    private static string? GetHtmlAttribute(string tag, string name)
    {
        var pattern = name + "\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)'|([^\\s>]+))";
        var m = Regex.Match(tag, pattern, RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        for (var i = 1; i <= 3; i++)
        {
            if (m.Groups[i].Success) return m.Groups[i].Value;
        }
        return null;
    }
}
