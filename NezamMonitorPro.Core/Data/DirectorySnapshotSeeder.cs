using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace NezamMonitorPro.Core.Data;

// ==================== SEEDER اسنپ‌شات دفترچه (پورت اندروید v3.11.3) ====================
// کل دفترچهٔ دفاتر طراحی و مهندسین یزد داخلِ خودِ برنامه، به‌صورت
// directory-snapshot.json تعبیه شده است (API رسمی وجود ندارد). در اولین
// اجرا — و هر وقت اسنپ‌شاتِ جدیدتری بیاید — جدول‌های دفترچه از آن پر
// می‌شوند، بدون هیچ درخواست شبکه‌ای. به‌روزرسانیِ درون‌برنامه‌ای وجود
// ندارد (تصمیم کاربر ۲۰۲۶-۱۱-۲۰): اسنپ‌شاتِ جدیدتر کلِ مجموعه‌داده را
// جایگزین می‌کند؛ جدول‌ها خالی و دوباره پر می‌شوند. هیچ ادغامِ
// رکوردبه‌رکوردی نیست.
// نسخهٔ 3.11.4 اندروید: اسنپ‌شات با خطِ عربی سایت است (بیشتر نام
// مهندس‌ها و شهر/آدرس/مدیر دفترها). همهٔ متن‌های دفترچه هنگامِ seed به
// فارسی تبدیل می‌شوند تا خودِ جدول‌ها فارسی باشند. URLها
// (detailUrl و photo) هرگز تبدیل نمی‌شوند.
// دفترهای شبح (104، 116، 23، 29، 37، 44، 64، 87) و 78/89 اصلا در
// اسنپ‌شات نیستند (صفحهٔ جزئیات ندارند)، پس همچنان «یافت نشد»
// می‌مانند — هیچ جایگزینی‌ای در کار نیست.
public static class DirectorySnapshotSeeder
{
    /// <summary>نامِ دقیقِ منبعِ تعبیه‌شدهٔ اسنپ‌شات در اسمبلی.</summary>
    public const string ResourceName = "NezamMonitorPro.Core.Data.directory-snapshot.json";

    /// <summary>نتیجهٔ شمارشِ یک seed موفق.</summary>
    public sealed record SeedResult(int Offices, int Engineers, int ListOnly, int EducationRows, int HistoryRows);

    /// <summary>نتیجهٔ EnsureSeeded؛ با pattern matching مصرف می‌شود.</summary>
    public abstract record SeedOutcome
    {
        /// <summary>همین نسخهٔ اسنپ‌شات قبلا seed شده بود.</summary>
        public sealed record AlreadySeeded : SeedOutcome;

        /// <summary>seed با موفقیت انجام شد.</summary>
        public sealed record Seeded(string Version, SeedResult Result) : SeedOutcome;

        /// <summary>seed انجام نشد؛ Reason علت را می‌گوید.</summary>
        public sealed record Failed(string Reason) : SeedOutcome;
    }

    private static readonly Regex GeneratedRegex = new("\"generated\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    // ---- شکل‌های JSON اسنپ‌شات (کلیدهای snake_case مثل خودِ فایل) ----

    private sealed class SnapshotMeta
    {
        [JsonPropertyName("source")]
        public string Source { get; set; } = "";
        [JsonPropertyName("generated")]
        public string Generated { get; set; } = "";
    }

    private sealed class SnapshotOffice
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("licence")]
        public string Licence { get; set; } = "";
        [JsonPropertyName("manager")]
        public string Manager { get; set; } = "";
        [JsonPropertyName("city")]
        public string City { get; set; } = "";
        [JsonPropertyName("province")]
        public string Province { get; set; } = "";
        [JsonPropertyName("detail_url")]
        public string DetailUrl { get; set; } = "";
        [JsonPropertyName("mobile")]
        public string Mobile { get; set; } = "";
        [JsonPropertyName("contact")]
        public string Contact { get; set; } = "";
        [JsonPropertyName("address")]
        public string Address { get; set; } = "";
        [JsonPropertyName("phone")]
        public string Phone { get; set; } = "";
        [JsonPropertyName("fax")]
        public string Fax { get; set; } = "";
        [JsonPropertyName("hours")]
        public string Hours { get; set; } = "";
        [JsonPropertyName("members")]
        public List<int> Members { get; set; } = new();
    }

    private sealed class SnapshotHistory
    {
        [JsonPropertyName("kind")]
        public string Kind { get; set; } = "";
        [JsonPropertyName("grade1")]
        public string Grade1 { get; set; } = "";
        [JsonPropertyName("grade2")]
        public string Grade2 { get; set; } = "";
        [JsonPropertyName("grade3")]
        public string Grade3 { get; set; } = "";
    }

    private sealed class SnapshotEducation
    {
        [JsonPropertyName("degree")]
        public string Degree { get; set; } = "";
        [JsonPropertyName("field")]
        public string Field { get; set; } = "";
        [JsonPropertyName("orientation")]
        public string Orientation { get; set; } = "";
        [JsonPropertyName("university_type")]
        public string UniversityType { get; set; } = "";
        [JsonPropertyName("university")]
        public string University { get; set; } = "";
        [JsonPropertyName("city")]
        public string City { get; set; } = "";
        [JsonPropertyName("country")]
        public string Country { get; set; } = "";
        [JsonPropertyName("start")]
        public string Start { get; set; } = "";
        [JsonPropertyName("end")]
        public string End { get; set; } = "";
    }

    private sealed class SnapshotEngineer
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("family")]
        public string Family { get; set; } = "";
        [JsonPropertyName("discipline")]
        public string Discipline { get; set; } = "";
        [JsonPropertyName("phone")]
        public string Phone { get; set; } = "";
        [JsonPropertyName("design_grade")]
        public string DesignGrade { get; set; } = "";
        [JsonPropertyName("supervision_grade")]
        public string SupervisionGrade { get; set; } = "";
        [JsonPropertyName("execution_grade")]
        public string ExecutionGrade { get; set; } = "";
        [JsonPropertyName("licence_issue")]
        public string LicenceIssue { get; set; } = "";
        [JsonPropertyName("licence_validity")]
        public string LicenceValidity { get; set; } = "";
        [JsonPropertyName("photo")]
        public string Photo { get; set; } = "";
        [JsonPropertyName("licence_history")]
        public List<SnapshotHistory> LicenceHistory { get; set; } = new();
        [JsonPropertyName("education")]
        public List<SnapshotEducation> Education { get; set; } = new();
        [JsonPropertyName("office_id")]
        public int OfficeId { get; set; }
        [JsonPropertyName("office_name")]
        public string OfficeName { get; set; } = "";
    }

    private sealed class SnapshotListEngineer
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
        [JsonPropertyName("family")]
        public string Family { get; set; } = "";
        [JsonPropertyName("discipline")]
        public string Discipline { get; set; } = "";
        [JsonPropertyName("design_grade")]
        public string DesignGrade { get; set; } = "";
        [JsonPropertyName("supervision_grade")]
        public string SupervisionGrade { get; set; } = "";
        [JsonPropertyName("execution_grade")]
        public string ExecutionGrade { get; set; } = "";
        // در فایل، office_nameِ بیشتر ردیف‌های فقط-فهرستی null است.
        [JsonPropertyName("office_name")]
        public string? OfficeName { get; set; }
    }

    private sealed class SnapshotFile
    {
        [JsonPropertyName("meta")]
        public SnapshotMeta Meta { get; set; } = new();
        [JsonPropertyName("offices")]
        public List<SnapshotOffice> Offices { get; set; } = new();
        [JsonPropertyName("engineers")]
        public Dictionary<string, SnapshotEngineer> Engineers { get; set; } = new();
        [JsonPropertyName("engineers_list_only")]
        public List<SnapshotListEngineer> EngineersListOnly { get; set; } = new();
    }

    /// <summary>
    /// نسخهٔ اسنپ‌شات ("yyyy-MM-dd") از بایت‌های اولِ فایل — بلوکِ meta
    /// بالای فایل است، پس برای بررسی نسخه لازم نیست کلِ فایلِ ۱٫۹
    /// مگابایتی خوانده شود. null وقتی پیدا نشد.
    /// </summary>
    public static string? ReadSnapshotVersion(string head)
    {
        if (string.IsNullOrEmpty(head)) return null;
        var match = GeneratedRegex.Match(head);
        if (!match.Success) return null;
        var version = match.Groups[1].Value;
        return string.IsNullOrWhiteSpace(version) ? null : version;
    }

    /// <summary>پایهٔ «_» در فهرست مهندسین یعنی «پایه ندارد».</summary>
    internal static string CleanGrade(string grade) => grade.Trim() == "_" ? "" : grade;

    /// <summary>حروف عربی به فارسی (ي→ی، ك→ک) — روی همهٔ متن‌های دفترچه هنگام seed.</summary>
    internal static string Persian(string text) => DirectoryParser.ToPersianChars(text);

    /// <summary>
    /// جدول‌های دفترچه را از اسنپ‌شاتِ تعبیه‌شده پر می‌کند، وقتی نسخه‌اش
    /// جدیدتر از نسخهٔ قبلا seedشده باشد (یا هنوز چیزی seed نشده باشد).
    /// هرگز throw نمی‌کند و هیچ شبکه‌ای لمس نمی‌کند. امن است که در هر
    /// شروعِ برنامه صدا زده شود — بررسی نسخه فقط بایت‌های اولِ منبع را
    /// می‌خواند و وقتی کاری نیست زود برمی‌گردد.
    /// </summary>
    public static SeedOutcome EnsureSeeded(NezamDatabase db)
    {
        try
        {
            if (db == null) throw new ArgumentNullException(nameof(db));

            byte[] bytes;
            using (var stream = typeof(DirectorySnapshotSeeder).Assembly.GetManifestResourceStream(ResourceName))
            {
                if (stream == null)
                    return new SeedOutcome.Failed("فایل اسنپ‌شات در برنامه پیدا نشد");
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                bytes = ms.ToArray();
            }

            var head = Encoding.UTF8.GetString(bytes, 0, Math.Min(4096, bytes.Length));
            var version = ReadSnapshotVersion(head);
            if (version == null)
                return new SeedOutcome.Failed("نسخهٔ اسنپ‌شات خوانده نشد");

            var stored = db.GetSetting(SettingKeys.DirectorySnapshotVersion, "") ?? "";
            if (stored == version) return new SeedOutcome.AlreadySeeded();
            // نسخهٔ ذخیره‌شدهٔ جدیدتر از فایل هرگز نباید تنزل کند (مثلا
            // فایل با نسخهٔ قدیمی‌تری جایگزین شده باشد).
            if (stored.Length != 0 && string.CompareOrdinal(stored, version) > 0)
                return new SeedOutcome.AlreadySeeded();

            var json = Encoding.UTF8.GetString(bytes);
            var snapshot = JsonSerializer.Deserialize<SnapshotFile>(json, JsonOptions)
                ?? throw new InvalidOperationException("اسنپ‌شات خالی است");

            var result = SeedFromParsed(snapshot, new DirectoryStore(db));

            // نسخه فقط بعد از seedِ موفق ذخیره می‌شود؛ اگر وسطِ seed
            // مشکلی پیش بیاید، شروعِ بعدی دوباره seed می‌کند.
            db.SaveSetting(SettingKeys.DirectorySnapshotVersion, version);
            return new SeedOutcome.Seeded(version, result);
        }
        catch (Exception ex)
        {
            var reason = ex.Message;
            if (string.IsNullOrWhiteSpace(reason)) reason = ex.GetType().Name;
            return new SeedOutcome.Failed(reason);
        }
    }

    /// <summary>
    /// از اسنپ‌شاتِ تجزیه‌شده seed می‌کند. کلِ مجموعه‌داده جایگزین
    /// می‌شود: اول هر ۴ جدول خالی، بعد همهٔ ردیف‌ها درج می‌شوند.
    /// </summary>
    private static SeedResult SeedFromParsed(SnapshotFile snapshot, DirectoryStore store)
    {
        store.DeleteAllDirectory();

        var officesToSeed = snapshot.Offices
            .Where(o => o.Id != 0)
            .Select(o => new DirectoryOffice(
                SiteId: o.Id,
                Name: Persian(o.Name),
                Licence: Persian(o.Licence),
                Manager: Persian(o.Manager),
                Mobile: Persian(o.Mobile),
                Contact: Persian(o.Contact),
                Province: Persian(o.Province),
                City: Persian(o.City),
                Address: Persian(o.Address),
                Phone: Persian(o.Phone),
                Fax: Persian(o.Fax),
                Hours: Persian(o.Hours),
                DetailUrl: o.DetailUrl,
                MemberCount: (o.Members ?? new List<int>()).Count,
                UpdatedAt: ""))
            .ToList();
        if (officesToSeed.Count != 0) store.InsertOffices(officesToSeed);

        var engineersToSeed = new List<DirectoryEngineer>();
        var educationRows = new List<DirectoryEducation>();
        var historyRows = new List<DirectoryLicenceHistory>();
        var seededEngineerIds = new HashSet<int>();
        foreach (var eng in (snapshot.Engineers ?? new Dictionary<string, SnapshotEngineer>()).Values)
        {
            if (eng.Id == 0) continue;
            engineersToSeed.Add(new DirectoryEngineer(
                SiteId: eng.Id,
                Name: Persian(eng.Name),
                Family: Persian(eng.Family),
                Discipline: Persian(eng.Discipline),
                Phone: Persian(eng.Phone),
                DesignGrade: Persian(eng.DesignGrade),
                SupervisionGrade: Persian(eng.SupervisionGrade),
                ExecutionGrade: Persian(eng.ExecutionGrade),
                LicenceIssue: Persian(eng.LicenceIssue),
                LicenceValidity: Persian(eng.LicenceValidity),
                Photo: eng.Photo,
                OfficeId: eng.OfficeId,
                OfficeName: Persian(eng.OfficeName),
                UpdatedAt: ""));
            seededEngineerIds.Add(eng.Id);
            foreach (var edu in eng.Education ?? new List<SnapshotEducation>())
            {
                educationRows.Add(new DirectoryEducation(
                    Id: 0,
                    EngineerId: eng.Id,
                    Degree: Persian(edu.Degree),
                    Field: Persian(edu.Field),
                    Orientation: Persian(edu.Orientation),
                    UniversityType: Persian(edu.UniversityType),
                    University: Persian(edu.University),
                    City: Persian(edu.City),
                    Country: Persian(edu.Country),
                    Start: Persian(edu.Start),
                    End: Persian(edu.End)));
            }
            foreach (var hist in eng.LicenceHistory ?? new List<SnapshotHistory>())
            {
                historyRows.Add(new DirectoryLicenceHistory(
                    Id: 0,
                    EngineerId: eng.Id,
                    Kind: Persian(hist.Kind),
                    Grade1: Persian(hist.Grade1),
                    Grade2: Persian(hist.Grade2),
                    Grade3: Persian(hist.Grade3)));
            }
        }

        // مهندسینِ فقط-فهرستی: وقتی شناسه در هر دو جا هست، رکوردِ کامل
        // برنده است (معمولا نمی‌تواند پیش بیاید — دو مجموعه ذاتا جدا
        // هستند — ولی جزئیات کامل هرگز نباید به ردیفِ فقط-فهرستی تنزل کند).
        var listOnlyCount = 0;
        foreach (var eng in snapshot.EngineersListOnly ?? new List<SnapshotListEngineer>())
        {
            if (eng.Id == 0 || seededEngineerIds.Contains(eng.Id)) continue;
            engineersToSeed.Add(new DirectoryEngineer(
                SiteId: eng.Id,
                Name: Persian(eng.Name),
                Family: Persian(eng.Family),
                Discipline: Persian(eng.Discipline),
                Phone: "",
                DesignGrade: CleanGrade(Persian(eng.DesignGrade)),
                SupervisionGrade: CleanGrade(Persian(eng.SupervisionGrade)),
                ExecutionGrade: CleanGrade(Persian(eng.ExecutionGrade)),
                LicenceIssue: "",
                LicenceValidity: "",
                Photo: "",
                OfficeId: 0,
                OfficeName: Persian(eng.OfficeName ?? ""),
                UpdatedAt: ""));
            listOnlyCount++;
        }

        if (engineersToSeed.Count != 0) store.InsertEngineers(engineersToSeed);
        if (educationRows.Count != 0) store.InsertEducation(educationRows);
        if (historyRows.Count != 0) store.InsertLicenceHistory(historyRows);

        return new SeedResult(
            Offices: officesToSeed.Count,
            Engineers: seededEngineerIds.Count,
            ListOnly: listOnlyCount,
            EducationRows: educationRows.Count,
            HistoryRows: historyRows.Count);
    }
}
