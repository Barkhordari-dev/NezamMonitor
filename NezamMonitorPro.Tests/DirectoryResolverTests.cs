using NezamMonitorPro.Core.Data;

namespace NezamMonitorPro.Tests;

/// <summary>v12.7: the Yazd design-offices directory port — resolver +
/// display helpers, proven on the REAL embedded snapshot (203 offices,
/// 5,079 engineers), never on hand-made data. The snapshot is the one
/// bundled in Core (meta.generated 2026-11-20). Tests compile on Linux
/// but cannot RUN here (testhost never connects); the user runs
/// `dotnet test` on Windows.</summary>
public class DirectoryResolverTests
{
    private const string SnapshotResource = "NezamMonitorPro.Core.Data.directory-snapshot.json";

    // ---------- Embedded snapshot ----------

    [Fact]
    public void EmbeddedSnapshotResource_Exists()
    {
        // Fail loudly if the stream is null: that means the Core side
        // has not embedded directory-snapshot.json — never work around
        // it by copying the JSON into the Tests project.
        Assert.Equal(DirectorySnapshotSeeder.ResourceName, SnapshotResource);
        using var stream = typeof(DirectoryParser).Assembly
            .GetManifestResourceStream(SnapshotResource);
        Assert.True(stream != null,
            "Embedded snapshot resource '" + SnapshotResource + "' not found in the Core assembly.");
        Assert.True(stream!.Length > 1_000_000,
            $"Embedded snapshot is suspiciously small: {stream.Length} bytes.");
    }

    // ---------- Pure normalization ----------

    [Fact]
    public void ToPersianChars_ArabicLettersToPersian()
    {
        // Explicit escapes: input is Arabic ي (U+064A) / ك (U+0643),
        // expected output is Persian ی (U+06CC) / ک (U+06A9).
        Assert.Equal("ام\u06CCرحس\u06CCن کر\u06CCم\u06CC",
            DirectoryParser.ToPersianChars("ام\u064Aرحس\u064Aن كر\u064Aم\u064A"));
        Assert.Equal("", DirectoryParser.ToPersianChars(""));
        Assert.Equal("", DirectoryParser.ToPersianChars(null));
    }

    [Theory]
    [InlineData("۱۹۴", "194")]
    [InlineData("١٩٤", "194")]
    [InlineData("194", "194")]
    [InlineData("۱۳۹۱/۱۲/۲۳", "1391/12/23")]
    public void NormalizeDigits_PersianAndArabicToLatin(string input, string expected)
        => Assert.Equal(expected, DirectoryParser.NormalizeDigits(input));

    [Theory]
    [InlineData("ام\u064Aرحس\u064Aن  كر\u064Aم\u064A", "ام\u06CCرحس\u06CCن کر\u06CCم\u06CC")]
    [InlineData("دفتر  ۱۹۴", "دفتر 194")]
    [InlineData("تأیید", "تایید")]
    public void NormalizeForCompare_FoldsArabicLettersDigitsSpaces(string input, string expected)
        => Assert.Equal(expected, DirectoryParser.NormalizeForCompare(input));

    [Fact]
    public void CleanDisplay_CollapsesWhitespaceAndPersianizes()
    {
        Assert.Equal("دفتر 194",
            DirectoryParser.CleanDisplay("  دفتر   194  "));
        Assert.Equal("ام\u06CCرحس\u06CCن",
            DirectoryParser.CleanDisplay("ام\u064Aرحس\u064Aن"));
    }

    [Fact]
    public void CleanCode_LatinDigitsAndPersianLetters()
    {
        Assert.Equal("1391/12/23", DirectoryParser.CleanCode("  ۱۳۹۱/۱۲/۲۳ "));
        Assert.Equal("09133587127", DirectoryParser.CleanCode("۰۹۱۳۳۵۸۷۱۲۷"));
    }

    // ---------- Office query name ----------

    [Theory]
    [InlineData("شماره 194 يزد", "دفتر 194")]
    [InlineData("۱۹۴", "دفتر 194")]
    [InlineData("194", "دفتر 194")]
    [InlineData("شماره205يزد", "دفتر 205")]
    [InlineData("دفتر221 يزد", "دفتر 221")]
    [InlineData("دفتر شماره78", "دفتر 78")]
    [InlineData("دفتر طراحي267", "دفتر 267")]
    [InlineData("دفتر 194", "دفتر 194")]
    [InlineData("شرکت آریا 123", "شرکت آریا 123")]
    [InlineData("مهندسين مشاور امين", "مهندسين مشاور امين")]
    [InlineData("", "")]
    public void OfficeQueryName_Theory(string raw, string expected)
        => Assert.Equal(expected, DirectoryParser.OfficeQueryName(raw));

    // NOTE on the two guard rows above: no snapshot office name contains
    // digits in a company-style name — all 193 digit names are "دفتر N"
    // and the other 10 have no digits at all — so "شرکت آریا 123" is
    // synthetic (the guard it pins is real: a digit-containing company
    // name must never be rewritten into "دفتر N"). "مهندسين مشاور امين"
    // is a real snapshot office (siteId 475), unchanged as-is.

    [Theory]
    [InlineData("شماره 194 يزد", "194")]
    [InlineData("۱۹۴", "194")]
    [InlineData("دفتر", null)]
    [InlineData("", null)]
    public void ExtractNumber_Theory(string? raw, string? expected)
        => Assert.Equal(expected, DirectoryParser.ExtractNumber(raw));

    [Fact]
    public void ExtractNumber_Null_ReturnsNull()
        => Assert.Null(DirectoryParser.ExtractNumber(null));

    [Theory]
    [InlineData("مهندس مهدیه قپاني", "مهدیه قپانی")]
    [InlineData("مهندس ام\u064Aرحس\u064Aن كر\u064Aم\u064A", "ام\u06CCرحس\u06CCن کر\u06CCم\u06CC")]
    [InlineData("مهندس\u200Cمهدیه", "مهدیه")]
    [InlineData("مهندسان مشاور امین", "مهندسان مشاور امین")]
    [InlineData("مهدیه قپاني", "مهدیه قپانی")]
    public void StripEngineerTitle_Theory(string raw, string expected)
        => Assert.Equal(expected, DirectoryParser.StripEngineerTitle(raw));

    // ---------- Display helpers (v3.11.6 uniform display) ----------

    [Theory]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("_", true)]
    [InlineData("-", true)]
    [InlineData("دفتر 194", false)]
    [InlineData("0", false)]
    public void IsMissingDirectoryValue_Theory(string value, bool expected)
        => Assert.Equal(expected, DirectoryParser.IsMissingDirectoryValue(value));

    [Fact]
    public void IsMissingDirectoryValue_Null_IsMissing()
        => Assert.True(DirectoryParser.IsMissingDirectoryValue(null));

    [Theory]
    [InlineData("", "ثبت نشده")]
    [InlineData("_", "ثبت نشده")]
    [InlineData("-", "ثبت نشده")]
    [InlineData("دفتر 194", "دفتر 194")]
    [InlineData("  دفتر 194  ", "دفتر 194")]
    public void DirectoryDisplayValue_Theory(string value, string expected)
        => Assert.Equal(expected, DirectoryParser.DirectoryDisplayValue(value));

    [Theory]
    [InlineData("1391/12/23", true)]
    [InlineData("1300/01/01", true)]
    [InlineData("1278/10/11", false)]
    [InlineData("13285/02/0", false)]
    [InlineData("1391/13/01", false)]
    [InlineData("1391/12/32", false)]
    [InlineData("_", false)]
    [InlineData("", false)]
    [InlineData("1391-12-23", false)]
    public void IsPlausibleJalaliDate_Theory(string value, bool expected)
        => Assert.Equal(expected, DirectoryParser.IsPlausibleJalaliDate(value));

    [Theory]
    [InlineData("1391/12/23", "1391/12/23")]
    [InlineData("1278/10/11", null)]
    [InlineData("_", null)]
    [InlineData("", null)]
    public void LicenceGradeDate_Theory(string? value, string? expected)
        => Assert.Equal(expected, DirectoryParser.LicenceGradeDate(value));

    [Fact]
    public void LicenceGradeDate_Null_ReturnsNull()
        => Assert.Null(DirectoryParser.LicenceGradeDate(null));

    [Theory]
    [InlineData("1390/01/01", "1394/01/01", "1390/01/01 تا 1394/01/01")]
    [InlineData("1300/01/01", "1381/10/20", "تا 1381/10/20")]
    [InlineData("1390/01/01", "1300/01/01", "از 1390/01/01")]
    [InlineData("1300/01/01", "1300/01/01", "تاریخ ثبت نشده")]
    [InlineData("", "", "تاریخ ثبت نشده")]
    public void EducationDateText_Theory(string start, string end, string expected)
        => Assert.Equal(expected, DirectoryParser.EducationDateText(start, end));

    [Theory]
    [InlineData("1391", "۱۳۹۱")]
    [InlineData("194", "۱۹۴")]
    [InlineData("دفتر 194", "دفتر ۱۹۴")]
    public void ToPersianDigits_Theory(string value, string expected)
        => Assert.Equal(expected, DirectoryParser.ToPersianDigits(value));

    [Fact]
    public void Constants_BaseUrlAndPlaceholderPhoto()
    {
        Assert.Equal("http://service.yazdnezam.ir:8033", DirectoryParser.BaseUrl);
        Assert.Equal("images.png", DirectoryParser.PlaceholderPhoto);
    }

    // ---------- Profile photo from HTML ----------

    [Theory]
    [InlineData("<img class=\"img-fluid\" src=\"/images/images.png\">")]
    [InlineData("<img src=\"/images/images.png\" class=\"img-fluid\">")]
    [InlineData("<img class=\"img-fluid\" src=\"http://example.com/x/images.png\">")]
    public void ParseEngineerPhotoFromHtml_PlaceholderReturnsEmpty(string html)
        => Assert.Equal("", DirectoryParser.ParseEngineerPhotoFromHtml(html));

    [Theory]
    [InlineData("<img class=\"img-fluid\" src=\"/resume/Resume/public/uploads/img/foo.jpg\">")]
    [InlineData("<img src=\"/resume/Resume/public/uploads/img/foo.jpg\" class=\"img-fluid\">")]
    public void ParseEngineerPhotoFromHtml_RelativePhotoReturnsAbsoluteUrl(string html)
        => Assert.Equal("http://service.yazdnezam.ir:8033/resume/Resume/public/uploads/img/foo.jpg",
            DirectoryParser.ParseEngineerPhotoFromHtml(html));

    [Fact]
    public void ParseEngineerPhotoFromHtml_AbsolutePhotoReturnedAsIs()
    {
        const string url = "http://service.yazdnezam.ir:8033/resume/Resume/public/uploads/img/foo.jpg";
        Assert.Equal(url, DirectoryParser.ParseEngineerPhotoFromHtml(
            $"<img class=\"img-fluid\" src=\"{url}\">"));
    }

    [Theory]
    [InlineData("<img src=\"/x.jpg\">")]
    [InlineData("<img class=\"img-thumbnail\" src=\"/x.jpg\">")]
    [InlineData("")]
    [InlineData("<p>no image here</p>")]
    public void ParseEngineerPhotoFromHtml_NoImgFluidReturnsEmpty(string html)
        => Assert.Equal("", DirectoryParser.ParseEngineerPhotoFromHtml(html));

    // ---------- Seeded snapshot (real data) ----------

    /// <summary>A temp plain database seeded from the real embedded
    /// snapshot, with a DirectoryStore over the same DB. The full
    /// engineer list is read back with one plain SELECT: DirectoryStore
    /// has no GetAllEngineers (only GetAllOffices), and the resolver
    /// needs every row — including the 3,905 list-only engineers with
    /// no office.</summary>
    private sealed class SeededDirectory : IDisposable
    {
        public TestHelpers.TempDirectory Tmp { get; } = new();
        public NezamDatabase Db { get; }
        public DirectoryStore Store { get; }
        public DirectorySnapshotSeeder.SeedOutcome.Seeded FirstSeed { get; }
        public List<DirectoryOffice> Offices { get; }
        public List<DirectoryEngineer> Engineers { get; }

        public SeededDirectory()
        {
            Db = new NezamDatabase(Tmp.File("dir.db"));
            var outcome = DirectorySnapshotSeeder.EnsureSeeded(Db);
            if (outcome is DirectorySnapshotSeeder.SeedOutcome.Failed failed)
                Assert.Fail("EnsureSeeded failed: " + failed.Reason);
            FirstSeed = Assert.IsType<DirectorySnapshotSeeder.SeedOutcome.Seeded>(outcome);
            Store = new DirectoryStore(Db);
            Offices = Store.GetAllOffices();
            Engineers = LoadAllEngineers(Db);
        }

        private static List<DirectoryEngineer> LoadAllEngineers(NezamDatabase db)
        {
            var list = new List<DirectoryEngineer>();
            using var cmd = db.Connection.CreateCommand();
            cmd.CommandText = """
                SELECT SiteId, Name, Family, Discipline, Phone,
                    DesignGrade, SupervisionGrade, ExecutionGrade,
                    LicenceIssue, LicenceValidity, Photo, OfficeId,
                    OfficeName, UpdatedAt
                FROM directory_engineers ORDER BY SiteId ASC
                """;
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new DirectoryEngineer(
                    SiteId: r.GetInt32(r.GetOrdinal("SiteId")),
                    Name: r.GetString(r.GetOrdinal("Name")),
                    Family: r.GetString(r.GetOrdinal("Family")),
                    Discipline: r.GetString(r.GetOrdinal("Discipline")),
                    Phone: r.GetString(r.GetOrdinal("Phone")),
                    DesignGrade: r.GetString(r.GetOrdinal("DesignGrade")),
                    SupervisionGrade: r.GetString(r.GetOrdinal("SupervisionGrade")),
                    ExecutionGrade: r.GetString(r.GetOrdinal("ExecutionGrade")),
                    LicenceIssue: r.GetString(r.GetOrdinal("LicenceIssue")),
                    LicenceValidity: r.GetString(r.GetOrdinal("LicenceValidity")),
                    Photo: r.GetString(r.GetOrdinal("Photo")),
                    OfficeId: r.GetInt32(r.GetOrdinal("OfficeId")),
                    OfficeName: r.GetString(r.GetOrdinal("OfficeName")),
                    UpdatedAt: r.GetString(r.GetOrdinal("UpdatedAt"))));
            }
            return list;
        }

        public void Dispose()
        {
            Db.Dispose();
            Tmp.Dispose();
        }
    }

    [Fact]
    public void Seed_FirstCall_SeedsAllCounts()
    {
        using var dir = new SeededDirectory();
        Assert.Equal("2026-11-20", dir.FirstSeed.Version);
        var result = dir.FirstSeed.Result;
        Assert.Equal(203, result.Offices);
        Assert.Equal(1174, result.Engineers);
        Assert.Equal(3905, result.ListOnly);
        Assert.Equal(1511, result.EducationRows);
        Assert.Equal(3134, result.HistoryRows);
        Assert.Equal(203, dir.Store.GetOfficeCount());
        Assert.Equal(5079, dir.Store.GetEngineerCount());
        Assert.Equal(203, dir.Offices.Count);
        Assert.Equal(5079, dir.Engineers.Count);
    }

    [Fact]
    public void Seed_SecondCall_AlreadySeeded_CountsUnchanged()
    {
        using var dir = new SeededDirectory();
        var second = DirectorySnapshotSeeder.EnsureSeeded(dir.Db);
        Assert.IsType<DirectorySnapshotSeeder.SeedOutcome.AlreadySeeded>(second);
        Assert.Equal(203, dir.Store.GetOfficeCount());
        Assert.Equal(5079, dir.Store.GetEngineerCount());
    }

    [Fact]
    public void ResolveOffice_SampleCaseValues()
    {
        using var dir = new SeededDirectory();

        // The 7 distinct Office values of the 77-case sample
        // (~/workspace/nzmdata-sample/data.json).
        Assert.Equal(433, DirectoryParser.ResolveOffice("۱۹۴", dir.Offices)?.SiteId);
        Assert.Equal(433, DirectoryParser.ResolveOffice("شماره 194 يزد", dir.Offices)?.SiteId);
        Assert.Equal(464, DirectoryParser.ResolveOffice("شماره205يزد", dir.Offices)?.SiteId);
        Assert.Equal(482, DirectoryParser.ResolveOffice("دفتر221 يزد", dir.Offices)?.SiteId);

        // Ghost / missing offices: no detail page, no substitution.
        // «دفتر شماره 89» (17 cases) and «دفتر شماره78» (8 cases) do
        // not exist in the snapshot — same as 104 in Android 3.11.1.
        Assert.Null(DirectoryParser.ResolveOffice("دفتر شماره 89", dir.Offices));
        Assert.Null(DirectoryParser.ResolveOffice("۸۹", dir.Offices));
        Assert.Null(DirectoryParser.ResolveOffice("دفتر شماره78", dir.Offices));
        Assert.Null(DirectoryParser.ResolveOffice("۱۰۴", dir.Offices));

        // CORRECTION to the brief: «شماره258» (5 cases) and
        // «دفتر طراحي267» (1 case) ALSO have no office in the snapshot —
        // the highest office number is 240, and neither 258 nor 267 is
        // even in ghost-offices.json (ghosts are 104/116/23/29/37/44/
        // 64/87). The query name «دفتر 267» is built correctly (see
        // OfficeQueryName above), but there is no such office to resolve
        // to — so the answer is null, never a near office.
        Assert.Null(DirectoryParser.ResolveOffice("شماره258", dir.Offices));
        Assert.Null(DirectoryParser.ResolveOffice("دفتر طراحي267", dir.Offices));

        // A digit-containing company name with no دفتر/شماره marker
        // must never resolve to an office.
        Assert.Null(DirectoryParser.ResolveOffice("شرکت آریا 123", dir.Offices));
        Assert.Null(DirectoryParser.ResolveOffice("", dir.Offices));
        Assert.Null(DirectoryParser.ResolveOffice("   ", dir.Offices));
    }

    [Fact]
    public void ResolveOffice_194_HasTenMembers()
    {
        using var dir = new SeededDirectory();
        var office = DirectoryParser.ResolveOffice("۱۹۴", dir.Offices);
        Assert.NotNull(office);
        Assert.Equal(433, office!.SiteId);
        Assert.Equal("دفتر 194", office.Name);
        Assert.Equal(10, office.MemberCount);

        var stored = dir.Store.GetOffice(433);
        Assert.NotNull(stored);
        Assert.Equal("دفتر 194", stored!.Name);
        Assert.Equal(10, stored.MemberCount);
        // The seed converts Arabic script to Persian in the tables.
        Assert.Equal("محمد افخمی اردکانی", stored.Manager);
        Assert.Equal("http://service.yazdnezam.ir:8033/resume/daftar/433", stored.DetailUrl);

        var members = dir.Store.GetEngineersForOffice(433);
        Assert.Equal(10, members.Count);
        Assert.All(members, m => Assert.Equal(433, m.OfficeId));

        // The other two Ardakan offices from the sample.
        Assert.Equal(9, dir.Store.GetEngineersForOffice(464).Count);
        Assert.Equal(2, dir.Store.GetEngineersForOffice(482).Count);
    }

    [Fact]
    public void ResolveOffice_OldPlainComparison_DoesNotMatch194()
    {
        // The 3.11.3 bug proof: the clicked value «۱۹۴» is never equal
        // to any stored name, so a naive `Name == raw` lookup finds
        // nothing — the resolver's normalization is what makes it match.
        using var dir = new SeededDirectory();
        Assert.DoesNotContain(dir.Offices, o => o.Name == "۱۹۴");
        Assert.Null(dir.Offices.FirstOrDefault(o => o.Name == "۱۹۴"));
        Assert.Null(dir.Offices.FirstOrDefault(o => o.Name == "شماره 194 يزد"));
        Assert.NotNull(DirectoryParser.ResolveOffice("۱۹۴", dir.Offices));
    }

    [Fact]
    public void ResolveEngineer_Karimi_FullRecord_3320()
    {
        using var dir = new SeededDirectory();
        // Explicit escapes: the case API spells the name in Arabic
        // script (ي U+064A / ك U+0643); the seeded table is Persian.
        var eng = DirectoryParser.ResolveEngineer("ام\u064Aرحس\u064Aن كر\u064Aم\u064A", dir.Engineers);
        Assert.NotNull(eng);
        Assert.Equal(3320, eng!.SiteId);
        Assert.Equal("ام\u06CCرحس\u06CCن", eng.Name);
        Assert.Equal("کر\u06CCم\u06CC", eng.Family);
        Assert.Equal("برق", eng.Discipline);
        Assert.Equal(438, eng.OfficeId);
        Assert.Equal("دفتر 195", eng.OfficeName);

        var stored = dir.Store.GetEngineer(3320);
        Assert.NotNull(stored);
        Assert.Equal("برق", stored!.Discipline);

        // Education: 1 row, start is the site's 1300/01/01 "unknown".
        var education = dir.Store.GetEducation(3320);
        Assert.Single(education);
        Assert.Equal("کارشناسی", education[0].Degree);
        Assert.Equal("برق", education[0].Field);
        Assert.Equal("1300/01/01", education[0].Start);
        Assert.Equal("1381/10/20", education[0].End);
        Assert.Equal("تا 1381/10/20",
            DirectoryParser.EducationDateText(education[0].Start, education[0].End));

        // Licence history: 3 rows; the اجرا grade-3 date is the
        // garbage 1278/10/11 — LicenceGradeDate must reject it.
        var history = dir.Store.GetLicenceHistory(3320);
        Assert.Equal(3, history.Count);
        Assert.Contains(history, h => h.Kind == "پایه طراحی" && h.Grade3 == "1391/12/23");
        Assert.Contains(history, h => h.Kind == "پایه نظارت" && h.Grade3 == "1391/12/23");
        var execution = Assert.Single(history, h => h.Kind == "پایه اجرا");
        Assert.Equal("1278/10/11", execution.Grade3);
        Assert.Null(DirectoryParser.LicenceGradeDate(execution.Grade3));
    }

    [Fact]
    public void ResolveEngineer_WithTitle_ListOnly_7207()
    {
        using var dir = new SeededDirectory();
        // «مهدیه قپاني» is a LIST-ONLY engineer (id 7207): no detail
        // page, no office — but she is in the table, so she resolves.
        // The leading «مهندس» title must be stripped first.
        var eng = DirectoryParser.ResolveEngineer("مهندس مهدیه قپاني", dir.Engineers);
        Assert.NotNull(eng);
        Assert.Equal(7207, eng!.SiteId);
        Assert.Equal("مهدیه", eng.Name);
        Assert.Equal("قپانی", eng.Family);
        Assert.Equal("معماری", eng.Discipline);

        var stored = dir.Store.GetEngineer(7207);
        Assert.NotNull(stored);
        Assert.Equal(0, stored!.OfficeId);
        Assert.Equal("", stored.Photo);
        Assert.Equal("", stored.LicenceIssue);
        Assert.Equal("3", stored.DesignGrade);
        Assert.Equal("3", stored.SupervisionGrade);
        Assert.Equal("", stored.ExecutionGrade); // "_" cleaned at seed
        Assert.Empty(dir.Store.GetEducation(7207));
        Assert.Empty(dir.Store.GetLicenceHistory(7207));
    }

    [Fact]
    public void ResolveEngineer_ListOnly_Anahita_6949()
    {
        using var dir = new SeededDirectory();
        var eng = DirectoryParser.ResolveEngineer("اناهيتا آب حيات", dir.Engineers);
        Assert.NotNull(eng);
        Assert.Equal(6949, eng!.SiteId);
        Assert.Equal("اناه\u06CCتا", eng.Name);
        Assert.Equal("\u0622ب ح\u06CCات", eng.Family);
    }

    [Fact]
    public void Store_GetOfficeAndEngineer_BySiteId()
    {
        using var dir = new SeededDirectory();
        Assert.Equal(203, dir.Store.GetOfficeCount());
        Assert.Equal(5079, dir.Store.GetEngineerCount());

        Assert.Equal("دفتر 205", dir.Store.GetOffice(464)?.Name);
        Assert.Equal(9, dir.Store.GetOffice(464)?.MemberCount);
        Assert.Equal("دفتر 221", dir.Store.GetOffice(482)?.Name);
        Assert.Equal(2, dir.Store.GetOffice(482)?.MemberCount);
        Assert.Null(dir.Store.GetOffice(999999));
        Assert.Null(dir.Store.GetEngineer(999999));

        // A stored photo URL is kept absolute and untouched by seeding.
        var withPhoto = dir.Store.GetEngineer(4);
        Assert.NotNull(withPhoto);
        Assert.Equal(
            "http://service.yazdnezam.ir:8033/resume/Resume/public/uploads/img/ftpscanmohandesin/Pic/31-1-0-00103.jpg",
            withPhoto!.Photo);
    }
}
