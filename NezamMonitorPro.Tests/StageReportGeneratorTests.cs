using System.IO.Compression;
using System.Xml.Linq;
using NezamMonitorPro.Core.Models;
using NezamMonitorPro.Core.Reports;

namespace NezamMonitorPro.Tests;

/// <summary>TEST-002: the heart of the app finally has tests. Verifies the
/// span-aware replacement (W1 / Android 3.8.1) on a copy of the real
/// template templates/2.docx — runs are never added or removed, only runs
/// whose text changed are written back, and no placeholder survives.</summary>
public class StageReportGeneratorTests
{
    private static readonly XNamespace W =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    private static readonly string[] PlaceholdersInTemplate2 =
    {
        "«شماره_پرونده»", "«کد_نوسازی»", "«تاریخ_گزارش»", "«مالک»",
        "«نوع_کاربری»", "«شماره_پروانه»", "«عنوان_بلوک»", "«تاریخ_صدور_پروانه»"
    };

    private static XDocument LoadDocumentXml(string docxPath)
    {
        using var zip = ZipFile.OpenRead(docxPath);
        var entry = zip.GetEntry("word/document.xml")!;
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static List<XElement> Runs(XDocument doc) => doc.Descendants(W + "r").ToList();

    private static string RunText(XElement run) =>
        string.Concat(run.Elements(W + "t").Select(t => t.Value));

    [Fact]
    public void GenerateReport_SpanAware_RunsUnchangedExceptTouched()
    {
        var template = TestHelpers.FindRepoFile(Path.Combine("templates", "2.docx"));
        Assert.True(template != null, "templates/2.docx not found under the repo root.");

        using var tmp = new TestHelpers.TempDirectory();
        var templateCopy = tmp.File("2.docx");
        File.Copy(template!, templateCopy);

        // Baseline: every run's XML + the section properties, before generation.
        var beforeDoc = LoadDocumentXml(templateCopy);
        var beforeRuns = Runs(beforeDoc);
        var beforeRunXml = beforeRuns.Select(r => r.ToString(SaveOptions.DisableFormatting)).ToList();
        var beforeRunTexts = beforeRuns.Select(RunText).ToList();
        var beforeSectPr = beforeDoc.Descendants(W + "sectPr").Single().ToString(SaveOptions.DisableFormatting);
        var beforeText = string.Concat(beforeDoc.Descendants(W + "t").Select(t => t.Value));
        Assert.Contains("«شماره_پرونده»", beforeText);

        var generator = new StageReportGenerator(tmp.Path);
        var result = generator.GenerateReport(
            TestHelpers.SampleCase(), stage: 2, rowNumber: 1,
            outputDir: tmp.Path, now: new DateTime(2026, 11, 13),
            templatePathOverride: templateCopy);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.OutputPath);
        Assert.True(File.Exists(result.OutputPath));

        var afterDoc = LoadDocumentXml(result.OutputPath!);
        var afterRuns = Runs(afterDoc);
        var afterRunXml = afterRuns.Select(r => r.ToString(SaveOptions.DisableFormatting)).ToList();
        var afterText = string.Concat(afterDoc.Descendants(W + "t").Select(t => t.Value));

        // 1. Runs are never added or removed.
        Assert.Equal(beforeRuns.Count, afterRuns.Count);

        // 2. sectPr is byte-identical.
        var afterSectPr = afterDoc.Descendants(W + "sectPr").Single().ToString(SaveOptions.DisableFormatting);
        Assert.Equal(beforeSectPr, afterSectPr);

        // 3. No placeholder survives.
        foreach (var ph in PlaceholdersInTemplate2)
            Assert.DoesNotContain(ph, afterText);

        // 4. Values landed (Persian digits, no LTR reversal by default).
        Assert.Contains("سمیرا رجبی", afterText);
        Assert.Contains("۱۴۰۴/۵۱۴", afterText);

        // 5. Only runs that contained a placeholder changed; every other run
        //    is byte-identical to the template.
        var changed = 0;
        for (var i = 0; i < beforeRuns.Count; i++)
        {
            if (beforeRunXml[i] != afterRunXml[i])
            {
                changed++;
                Assert.Contains("«", beforeRunTexts[i]);
            }
            else
            {
                Assert.DoesNotContain("«", beforeRunTexts[i]);
            }
        }
        Assert.True(changed > 0, "Expected at least one run to change.");
        Assert.True(changed < beforeRuns.Count, "Only placeholder runs should change.");
    }

    [Fact]
    public void GenerateReport_MissingTemplate_FailsGracefully()
    {
        using var tmp = new TestHelpers.TempDirectory();
        var generator = new StageReportGenerator(tmp.Path);
        var result = generator.GenerateReport(
            TestHelpers.SampleCase(), stage: 2, rowNumber: 1,
            outputDir: tmp.Path, now: new DateTime(2026, 11, 13),
            templatePathOverride: tmp.File("does-not-exist.docx"));

        Assert.False(result.Success);
        Assert.NotEmpty(result.ErrorMessage);
    }

    [Fact]
    public void StageRules_FindMatchingReports_MatchesStageTitles()
    {
        var c = TestHelpers.SampleCase();
        Assert.Single(StageReportRules.FindMatchingReports(c, 1));
        Assert.Empty(StageReportRules.FindMatchingReports(c, 2));
    }

    /// <summary>v12.5: the logged-in user's name replaces "نام ناظر" in the
    /// template (straight quotes, as written in templates/2.docx). The name
    /// is deliberately different from the sample case's owner ("سمیرا رجبی")
    /// so the test proves the supervisor-name replacement, not the owner one.</summary>
    [Fact]
    public void GenerateReport_SupervisorName_ReplacesNamePlaceholder()
    {
        var template = TestHelpers.FindRepoFile(Path.Combine("templates", "2.docx"));
        Assert.True(template != null, "templates/2.docx not found under the repo root.");

        using var tmp = new TestHelpers.TempDirectory();
        var templateCopy = tmp.File("2.docx");
        File.Copy(template!, templateCopy);

        var beforeDoc = LoadDocumentXml(templateCopy);
        var beforeText = string.Concat(beforeDoc.Descendants(W + "t").Select(t => t.Value));
        Assert.Contains("نام ناظر", beforeText);

        var generator = new StageReportGenerator(tmp.Path)
        {
            UserName = "محمد مهدی برخورداری"
        };
        var result = generator.GenerateReport(
            TestHelpers.SampleCase(), stage: 2, rowNumber: 1,
            outputDir: tmp.Path, now: new DateTime(2026, 11, 13),
            templatePathOverride: templateCopy);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.OutputPath);

        var afterDoc = LoadDocumentXml(result.OutputPath!);
        var afterText = string.Concat(afterDoc.Descendants(W + "t").Select(t => t.Value));

        Assert.DoesNotContain("نام ناظر", afterText);
        Assert.Contains("محمد مهدی برخورداری", afterText);
    }

    /// <summary>v12.5: when no user name is known (fresh install, never
    /// extracted), the placeholder must stay visible in the output — never
    /// be blanked out.</summary>
    [Fact]
    public void GenerateReport_EmptyUserName_LeavesSupervisorPlaceholder()
    {
        var template = TestHelpers.FindRepoFile(Path.Combine("templates", "2.docx"));
        Assert.True(template != null, "templates/2.docx not found under the repo root.");

        using var tmp = new TestHelpers.TempDirectory();
        var templateCopy = tmp.File("2.docx");
        File.Copy(template!, templateCopy);

        var generator = new StageReportGenerator(tmp.Path);
        var result = generator.GenerateReport(
            TestHelpers.SampleCase(), stage: 2, rowNumber: 1,
            outputDir: tmp.Path, now: new DateTime(2026, 11, 13),
            templatePathOverride: templateCopy);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.OutputPath);

        var afterDoc = LoadDocumentXml(result.OutputPath!);
        var afterText = string.Concat(afterDoc.Descendants(W + "t").Select(t => t.Value));

        Assert.Contains("نام ناظر", afterText);
    }
}
