using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using NezamMonitorPro.Core.Models;
using NezamMonitorPro.Core.Reports;

namespace NezamMonitorPro.Tests;

/// <summary>v12.6: the step-by-step report wizard's content-driven template
/// logic, proven on the REAL templates 1–4.docx (never on a hand-made
/// fake). The wizard reads the checkbox table and the numbered توضیحات
/// items from the template file itself — so a future template may rename
/// its columns, move them, or rename the section and the wizard still
/// works. The mutated-template test pins exactly that.</summary>
public class ReportWizardLogicTests
{
    private static readonly XNamespace W =
        "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    private static readonly int[] ExpectedCheckboxRows = { 7, 11, 6, 6 };

    private static string TemplatePath(int stage)
    {
        var path = TestHelpers.FindRepoFile(Path.Combine("templates", $"{stage}.docx"));
        Assert.True(path != null, $"templates/{stage}.docx not found under the repo root.");
        return path!;
    }

    private static XDocument LoadDocumentXml(string docxPath)
    {
        using var zip = ZipFile.OpenRead(docxPath);
        var entry = zip.GetEntry("word/document.xml")!;
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static void SaveDocumentXml(string docxPath, XDocument doc)
    {
        using var zip = ZipFile.Open(docxPath, ZipArchiveMode.Update);
        var entry = zip.GetEntry("word/document.xml")!;
        using var stream = entry.Open();
        stream.SetLength(0);
        doc.Save(stream);
    }

    private static string CellText(XElement cell) =>
        string.Concat(cell.Descendants(W + "t").Select(t => t.Value));

    /// <summary>Replace a cell's whole text: the first text node gets the
    /// new value, the rest are emptied (the runs themselves stay).</summary>
    private static void SetCellText(XElement cell, string text)
    {
        var texts = cell.Descendants(W + "t").ToList();
        if (texts.Count == 0) return;
        texts[0].Value = text;
        for (var i = 1; i < texts.Count; i++) texts[i].Value = "";
    }

    private static string DocumentText(string docxPath)
    {
        var doc = LoadDocumentXml(docxPath);
        return string.Concat(doc.Descendants(W + "t").Select(t => t.Value));
    }

    // ---------- Real templates ----------

    [Theory]
    [InlineData(1, 7)]
    [InlineData(2, 11)]
    [InlineData(3, 6)]
    [InlineData(4, 6)]
    public void ParseRealTemplate_RowsHeadersDescriptions(int stage, int expectedRows)
    {
        var data = ReportWizardLogic.LoadWizardTemplateData(TemplatePath(stage));
        Assert.NotNull(data);
        Assert.NotNull(data!.CheckboxTable);
        Assert.Equal(expectedRows, data.CheckboxTable!.Rows.Count);
        // The status-column labels come from the file's own header row.
        Assert.Equal(3, data.CheckboxTable.Headers.Count);
        Assert.Equal("انجام شده", data.CheckboxTable.Headers[0]);
        // The توضیحات cell has two EMPTY numbered slots — and the parse
        // must find that cell, not the serial-number cell («109855-2032000»)
        // elsewhere in the file: a serial has digits after the dash, a
        // numbered item has a space (or nothing) after it.
        Assert.NotNull(data.DescriptionItems);
        Assert.Equal(2, data.DescriptionItems!.Count);
        Assert.All(data.DescriptionItems, item => Assert.Equal("", item));
    }

    [Fact]
    public void ParseRealTemplate_Template4HeaderSaysStage3()
    {
        // Template 4's header says «مرحله سوم» and its rows are identical
        // to template 3's — the file is read as-is, never guessed.
        var data = ReportWizardLogic.LoadWizardTemplateData(TemplatePath(4));
        Assert.NotNull(data?.CheckboxTable);
        Assert.Equal(6, data!.CheckboxTable!.Rows.Count);
        Assert.Equal("انجام شده", data.CheckboxTable.Headers[0]);
    }

    // ---------- Which fields the template actually has ----------

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void PresentFieldKeys_OnlyTemplatePlaceholders(int stage)
    {
        var data = ReportWizardLogic.LoadWizardTemplateData(TemplatePath(stage));
        Assert.NotNull(data);
        // The 13 «...» keys + «نام ناظر» (either quote form) = 14. The
        // other 18 editable keys are NOT in these templates, so the wizard
        // must not show them — they cannot change the report.
        Assert.Equal(14, data!.PresentFieldKeys.Count);
        Assert.Contains("«مالک»", data.PresentFieldKeys);
        Assert.Contains("«نام ناظر»", data.PresentFieldKeys);
        Assert.DoesNotContain("«تلفن_ناظر_مکانیک»", data.PresentFieldKeys);
        Assert.DoesNotContain("«مسولیت»", data.PresentFieldKeys);
        Assert.DoesNotContain("«آدرس»", data.PresentFieldKeys);
    }

    // ---------- Mutated template ----------

    [Fact]
    public void ParseMutatedTemplate_ContentDriven()
    {
        using var tmp = new TestHelpers.TempDirectory();
        var file = tmp.File("2-mutated.docx");
        File.Copy(TemplatePath(2), file);

        var doc = LoadDocumentXml(file);
        var tables = doc.Descendants(W + "tbl").ToList();

        // The checkbox table: the one carrying a ☐ cell.
        var checkboxTable = tables.First(t => t.Descendants(W + "t").Any(x => x.Value.Contains('☐')));
        var rows = checkboxTable.Elements(W + "tr").ToList();
        var firstDataRow = rows.First(r => r.Descendants(W + "t").Any(x => x.Value.Contains('☐')));
        var headerRow = rows[rows.IndexOf(firstDataRow) - 1];

        // (a) Rename a header label: «انجام شده» -> «تکمیل شده».
        var headerCells = headerRow.Elements(W + "tc").ToList();
        var doneCell = headerCells.First(c => CellText(c).Trim() == "انجام شده");
        SetCellText(doneCell, "تکمیل شده");

        // (b) Rename the «توضیحات» title so the fallback must find the
        // cell by its numbered items alone.
        var numberedRe = new Regex(@"^[۰-۹٠-٩0-9]+[-–—](\s|$)");
        var descCell = tables.SelectMany(t => t.Descendants(W + "tc"))
            .First(c => c.Elements(W + "p").Any(p =>
                numberedRe.IsMatch(CellText(p).Trim())));
        foreach (var para in descCell.Elements(W + "p"))
        {
            if (CellText(para).Trim().StartsWith("توضیحات"))
            {
                var texts = para.Descendants(W + "t").ToList();
                if (texts.Count > 0)
                {
                    texts[0].Value = "یادداشت‌ها:";
                    for (var i = 1; i < texts.Count; i++) texts[i].Value = "";
                }
            }
        }

        // (c) Change the first data row's description text.
        var firstCells = firstDataRow.Elements(W + "tc").ToList();
        var descCol = firstCells.FindIndex(c =>
        {
            var t = CellText(c).Trim();
            return t.Length > 0 && !t.Contains('☐') && !t.All(char.IsDigit);
        });
        Assert.True(descCol >= 0, "No description cell in the first data row.");
        SetCellText(firstCells[descCol], "شرح جهش‌یافتهٔ آزمایشی");

        // (d) Swap the description column with the first checkbox column
        // in every data row.
        var checkCol = firstCells.FindIndex(c => CellText(c).Contains('☐'));
        Assert.True(checkCol >= 0, "No checkbox cell in the first data row.");
        // Swap in EVERY row of the table — including the header row,
        // because a moved column carries its header cell with it.
        foreach (var row in rows)
        {
            var cells = row.Elements(W + "tc").ToList();
            if (cells.Count <= Math.Max(descCol, checkCol)) continue;
            var a = new XElement(cells[descCol]);
            var b = new XElement(cells[checkCol]);
            cells[descCol].ReplaceWith(b);
            cells[checkCol].ReplaceWith(a);
        }

        SaveDocumentXml(file, doc);

        var data = ReportWizardLogic.LoadWizardTemplateData(file);
        Assert.NotNull(data);
        Assert.Equal("تکمیل شده", data!.CheckboxTable!.Headers[0]);
        Assert.Equal(11, data.CheckboxTable.Rows.Count);
        Assert.Equal("شرح جهش‌یافتهٔ آزمایشی", data.CheckboxTable.Rows[0].Description);
        Assert.NotNull(data.DescriptionItems);
        Assert.Equal(2, data.DescriptionItems!.Count);
    }

    // ---------- v12.8: no hardcoded description lines + full text step ----------

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void SplitDescriptionCell_RealTemplates_PrefixItemsSuffix(int stage)
    {
        using var doc = WordprocessingDocument.Open(TemplatePath(stage), false);
        var split = ReportWizardLogic.SplitDescriptionCell(doc);
        Assert.NotNull(split);
        // Prefix: the «توضیحات:» title + the intro paragraph.
        Assert.True(split!.Value.Prefix.Count >= 2);
        Assert.StartsWith("توضیحات", split.Value.Prefix[0].Trim());
        // Items: the two EMPTY numbered slots of the real templates.
        Assert.Equal(2, split.Value.Items.Count);
        Assert.All(split.Value.Items, item => Assert.Equal("", item));
        // Suffix: the closing line.
        Assert.Single(split.Value.Suffix);
        Assert.True(split.Value.Suffix[0].Trim().Length > 0);
    }

    [Fact]
    public void BuildDescriptionFullText_RealTemplate2()
    {
        var data = ReportWizardLogic.LoadWizardTemplateData(TemplatePath(2));
        Assert.NotNull(data);
        var full = ReportWizardLogic.BuildDescriptionFullText(
            data!.DescriptionPrefixLines,
            new List<string> { "مورد اول", "مورد دوم" },
            data.DescriptionSuffixLines);
        Assert.StartsWith("توضیحات", full.Trim());
        Assert.Contains("۱- مورد اول", full);
        Assert.Contains("۲- مورد دوم", full);
        Assert.EndsWith(data.DescriptionSuffixLines[0].Trim(), full.Trim());
    }

    [Fact]
    public void ApplyDescriptionFullText_EditAddRemove()
    {
        using var tmp = new TestHelpers.TempDirectory();
        var file = tmp.File("3.docx");
        File.Copy(TemplatePath(3), file);
        using (var doc = WordprocessingDocument.Open(file, true))
        {
            var split = ReportWizardLogic.SplitDescriptionCell(doc);
            Assert.NotNull(split);
            var edited = ReportWizardLogic.BuildDescriptionFullText(
                split!.Value.Prefix,
                new List<string> { "مورد عوض‌شده", "مورد دوم", "مورد سوم تازه" },
                new List<string> { "خط پایانی عوض‌شده" });
            Assert.True(ReportWizardLogic.ApplyDescriptionFullText(doc, edited));
            doc.MainDocumentPart!.Document.Save();
        }
        using var doc2 = WordprocessingDocument.Open(file, false);
        var split2 = ReportWizardLogic.SplitDescriptionCell(doc2);
        Assert.NotNull(split2);
        Assert.Equal(3, split2!.Value.Items.Count);
        Assert.Equal("مورد عوض‌شده", split2.Value.Items[0]);
        Assert.Equal("مورد سوم تازه", split2.Value.Items[2]);
        Assert.Single(split2.Value.Suffix);
        Assert.Equal("خط پایانی عوض‌شده", split2.Value.Suffix[0].Trim());
        Assert.StartsWith("توضیحات", split2.Value.Prefix[0].Trim());
    }

    [Fact]
    public void ApplyDescriptionItems_InsertedRowKeepsRtlProperties()
    {
        // v12.9: the user added a 3rd description row in the wizard and
        // Word wrote it left-to-right. The template's numbered paragraphs
        // carry <w:bidi/> in their <w:pPr>; the inserted paragraph was
        // built bare (runs only), so Word rendered it LTR. The fix clones
        // the paragraph properties too.
        using var tmp = new TestHelpers.TempDirectory();
        var file = tmp.File("2.docx");
        File.Copy(TemplatePath(2), file);
        using (var doc = WordprocessingDocument.Open(file, true))
        {
            Assert.True(ReportWizardLogic.ApplyDescriptionItems(doc,
                new List<string> { "مورد اول", "مورد دوم", "مورد سوم" }));
            doc.MainDocumentPart!.Document.Save();
        }
        var xml = LoadDocumentXml(file);
        var paras = xml.Descendants(W + "p").ToList();
        var third = paras.First(p => (p.Value ?? "").Trim().StartsWith("۳- "));
        Assert.NotNull(third.Element(W + "pPr")?.Element(W + "bidi"));
        // The second row (rewritten in place) still has it too.
        var second = paras.First(p => (p.Value ?? "").Trim().StartsWith("۲- "));
        Assert.NotNull(second.Element(W + "pPr")?.Element(W + "bidi"));
    }

    [Fact]
    public void CheckboxHeaderBlank_FallsBackToDefaultLabel()
    {
        // The status labels come from the file's own header row — the
        // three defaults exist ONLY for a blank/missing header cell.
        using var tmp = new TestHelpers.TempDirectory();
        var file = tmp.File("2-blank-header.docx");
        File.Copy(TemplatePath(2), file);
        var doc = LoadDocumentXml(file);
        var tables = doc.Descendants(W + "tbl").ToList();
        var checkboxTable = tables.First(t => t.Descendants(W + "t").Any(x => x.Value.Contains('☐')));
        var rows = checkboxTable.Elements(W + "tr").ToList();
        var firstDataRow = rows.First(r => r.Descendants(W + "t").Any(x => x.Value.Contains('☐')));
        var headerRow = rows[rows.IndexOf(firstDataRow) - 1];
        var doneCell = headerRow.Elements(W + "tc").First(c => CellText(c).Trim() == "انجام شده");
        SetCellText(doneCell, "");
        SaveDocumentXml(file, doc);

        var data = ReportWizardLogic.LoadWizardTemplateData(file);
        Assert.NotNull(data?.CheckboxTable);
        Assert.Equal("انجام شده", data!.CheckboxTable!.Headers[0]);
        Assert.Equal(11, data.CheckboxTable.Rows.Count);
    }

    // ---------- End-to-end generation with overrides ----------

    [Fact]
    public void GenerateReport_WithWizardOverrides_LandsInFile()
    {
        using var tmp = new TestHelpers.TempDirectory();
        var templateCopy = tmp.File("2.docx");
        File.Copy(TemplatePath(2), templateCopy);

        var generator = new StageReportGenerator(tmp.Path) { UserName = "محمد مهدی برخورداری" };
        var overrides = new ReportWizardOverrides
        {
            FieldValues = new Dictionary<string, string>
            {
                ["«مالک»"] = "مالک ویرایش‌شدهٔ آزمایشی",
                ["«نام ناظر»"] = "ناظر ویرایش‌شده",
                ["«عنوان_بلوک»"] = "بلوک ویرایش‌شده",
            },
            CheckboxSelections = new Dictionary<int, int> { [0] = 0, [1] = 1 },
            DescriptionItems = new List<string> { "توضیح آزمایشی ۱", "توضیح آزمایشی ۲" },
        };

        var result = generator.GenerateReport(
            TestHelpers.SampleCase(), stage: 2, rowNumber: 1,
            outputDir: tmp.Path, now: new DateTime(2026, 11, 20),
            templatePathOverride: templateCopy, useLtr: false,
            wizardOverrides: overrides);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.OutputPath);
        Assert.True(result.WizardEditsApplied);
        Assert.Equal(2, result.WizardMarksApplied);

        // The file name carries the date with DASHES, never slashes:
        // a slash in a file name would build nested folders.
        var fileName = Path.GetFileName(result.OutputPath)!;
        Assert.Contains("۱۴۰۵-۰۸-۲۹", fileName);
        Assert.DoesNotContain("/", fileName);

        var text = DocumentText(result.OutputPath!);
        Assert.Contains("مالک ویرایش‌شدهٔ آزمایشی", text);
        Assert.Contains("ناظر ویرایش‌شده", text);
        Assert.Contains("بلوک ویرایش‌شده", text);
        Assert.Contains("توضیح آزمایشی ۱", text);
        Assert.Contains("توضیح آزمایشی ۲", text);
        Assert.DoesNotContain("«مالک»", text);
        Assert.DoesNotContain("«عنوان_بلوک»", text);
        // Exactly two black ticks, one per picked row.
        Assert.Equal(2, text.Count(ch => ch == '☑'));
    }

    [Fact]
    public void GenerateReport_WithFullTextOverride_WinsOverItems()
    {
        using var tmp = new TestHelpers.TempDirectory();
        var templateCopy = tmp.File("2.docx");
        File.Copy(TemplatePath(2), templateCopy);

        var generator = new StageReportGenerator(tmp.Path) { UserName = "محمد مهدی برخورداری" };
        var overrides = new ReportWizardOverrides
        {
            DescriptionItems = new List<string> { "ردیف یک", "ردیف دو" },
            DescriptionFullText = "توضیحات:\nمتن کامل آزمایشی\nخط پایانی آزمایشی",
        };

        var result = generator.GenerateReport(
            TestHelpers.SampleCase(), stage: 2, rowNumber: 1,
            outputDir: tmp.Path, now: new DateTime(2026, 11, 20),
            templatePathOverride: templateCopy, useLtr: false,
            wizardOverrides: overrides);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.WizardEditsApplied);
        var text = DocumentText(result.OutputPath!);
        Assert.Contains("متن کامل آزمایشی", text);
        Assert.Contains("خط پایانی آزمایشی", text);
        Assert.DoesNotContain("ردیف یک", text);
        Assert.DoesNotContain("ردیف دو", text);
    }

    [Fact]
    public void GenerateReport_EmptyDescriptionItems_LeavesSlots()
    {
        // No rows added in step 3 and no full-text edit: the template's
        // own (empty) numbered slots stay in the file, untouched.
        using var tmp = new TestHelpers.TempDirectory();
        var templateCopy = tmp.File("2.docx");
        File.Copy(TemplatePath(2), templateCopy);

        var generator = new StageReportGenerator(tmp.Path) { UserName = "محمد مهدی برخورداری" };
        var overrides = new ReportWizardOverrides
        {
            DescriptionItems = new List<string>(),
        };

        var result = generator.GenerateReport(
            TestHelpers.SampleCase(), stage: 2, rowNumber: 1,
            outputDir: tmp.Path, now: new DateTime(2026, 11, 20),
            templatePathOverride: templateCopy, useLtr: false,
            wizardOverrides: overrides);

        Assert.True(result.Success, result.ErrorMessage);
        var text = DocumentText(result.OutputPath!);
        Assert.Contains("۱-", text);
        Assert.Contains("۲-", text);
    }

    [Fact]
    public void GenerateReport_SupervisorOverrideWithoutUserName_AddsTheKey()
    {
        using var tmp = new TestHelpers.TempDirectory();
        var templateCopy = tmp.File("2.docx");
        File.Copy(TemplatePath(2), templateCopy);

        // No UserName known: the merge must ADD both quote forms of the
        // «نام ناظر» key, so the placeholder is still replaced.
        var generator = new StageReportGenerator(tmp.Path) { UserName = "" };
        var result = generator.GenerateReport(
            TestHelpers.SampleCase(), stage: 2, rowNumber: 1,
            outputDir: tmp.Path, now: new DateTime(2026, 11, 20),
            templatePathOverride: templateCopy, useLtr: false,
            wizardOverrides: new ReportWizardOverrides
            {
                FieldValues = new Dictionary<string, string>
                {
                    ["«نام ناظر»"] = "ناظر بدون نام کاربری",
                },
            });

        Assert.True(result.Success, result.ErrorMessage);
        var text = DocumentText(result.OutputPath!);
        Assert.Contains("ناظر بدون نام کاربری", text);
        Assert.DoesNotContain("نام ناظر", text);
    }

    [Fact]
    public void GenerateReport_NullDescriptions_LeavesTemplateUntouched()
    {
        using var tmp = new TestHelpers.TempDirectory();
        var templateCopy = tmp.File("2.docx");
        File.Copy(TemplatePath(2), templateCopy);

        var generator = new StageReportGenerator(tmp.Path) { UserName = "محمد مهدی برخورداری" };
        var result = generator.GenerateReport(
            TestHelpers.SampleCase(), stage: 2, rowNumber: 1,
            outputDir: tmp.Path, now: new DateTime(2026, 11, 20),
            templatePathOverride: templateCopy, useLtr: false,
            wizardOverrides: new ReportWizardOverrides
            {
                CheckboxSelections = new Dictionary<int, int> { [2] = 2 },
                DescriptionItems = null,
            });

        Assert.True(result.Success, result.ErrorMessage);
        var data = ReportWizardLogic.LoadWizardTemplateData(result.OutputPath!);
        Assert.NotNull(data?.DescriptionItems);
        Assert.Equal(2, data!.DescriptionItems!.Count);
        Assert.All(data.DescriptionItems, item => Assert.Equal("", item));
    }

    // ---------- Stage rule + suggestion ----------

    [Theory]
    [InlineData("الف", "عمران", 3)]
    [InlineData("ب", "شهرسازی", 3)]
    [InlineData("ج", "شهرسازی", 4)]
    [InlineData("د", "شهرسازی", 4)]
    [InlineData("ب", "عمران", 4)]
    [InlineData("د", "نقشه‌برداری", 4)]
    [InlineData("", "عمران", 3)]
    [InlineData("الف", "", 3)]
    public void StageCountForCase_FollowsScheduleRule(string group, string discipline, int expected)
        => Assert.Equal(expected, ReportWizardLogic.StageCountForCase(group, discipline));

    [Fact]
    public void SuggestedStage_FirstUnregistered()
    {
        Assert.Equal(3, ReportWizardLogic.SuggestedStage(3, new[] { 1, 2 }));
        Assert.Equal(0, ReportWizardLogic.SuggestedStage(3, new[] { 1, 2, 3 }));
        Assert.Equal(1, ReportWizardLogic.SuggestedStage(4, Array.Empty<int>()));
    }

    // ---------- Auto values + display ----------

    [Fact]
    public void AutoFieldValues_All32EditableKeysHaveValues()
    {
        using var tmp = new TestHelpers.TempDirectory();
        var generator = new StageReportGenerator(tmp.Path) { UserName = "محمد مهدی برخورداری" };
        var auto = generator.BuildAutoFieldValues(TestHelpers.SampleCase(), new DateTime(2026, 11, 20), useLtr: false);

        Assert.Equal(32, ReportWizardLogic.EditableFieldKeys.Count);
        foreach (var key in ReportWizardLogic.EditableFieldKeys)
            Assert.True(auto.ContainsKey(key), $"No auto value for {key}.");

        Assert.Equal("سمیرا رجبی", auto["«مالک»"]);
        Assert.Equal("محمد مهدی برخورداری", auto["«نام ناظر»"]);
        Assert.Equal("09222222222", auto["«تلفن_ناظر_مکانیک»"]);
        Assert.Equal("پایه ۱", auto["«صلاحیت_ناظر_مکانیک»"]);
        Assert.Equal("۱۴۰۵/۰۸/۲۹", auto["«تاریخ_گزارش»"]);
    }

    [Fact]
    public void AutoFieldValues_NoUserName_SupervisorKeyAbsent()
    {
        using var tmp = new TestHelpers.TempDirectory();
        var generator = new StageReportGenerator(tmp.Path) { UserName = "" };
        var auto = generator.BuildAutoFieldValues(TestHelpers.SampleCase(), new DateTime(2026, 11, 20), useLtr: false);

        // The placeholder stays visible, never blanked.
        Assert.False(auto.ContainsKey("«نام ناظر»"));
        Assert.False(auto.ContainsKey("\"نام ناظر\""));
    }

    [Fact]
    public void FieldLabel_KnownKeys()
    {
        Assert.Equal("کد نوسازی", ReportWizardLogic.FieldLabel("«کد_نوسازی»"));
        Assert.Equal("مهندس ناظر", ReportWizardLogic.FieldLabel("«نام ناظر»"));
    }

    [Fact]
    public void ToPersianDisplay_ArabicLettersAndDiacritics()
    {
        // Explicit escapes: input is Arabic ي (U+064A) / ك (U+0643), the
        // expected output is Persian ی (U+06CC) / ک (U+06A9).
        Assert.Equal("سم\u06CCرا کر\u06CCم\u06CC",
            ReportWizardLogic.ToPersianDisplay("سم\u064Aرا كر\u064Aم\u064A"));
        // أ is not a letter B Nazanin can show — it becomes ا, like the
        // v12.1 source sweep.
        Assert.Equal("تایید", ReportWizardLogic.ToPersianDisplay("تأیید"));
    }

    [Fact]
    public void InitialDescriptionItems_EmptyTemplateSlots_NothingInvented()
    {
        // v12.8: the two standard lines used to be hardcoded here as a
        // fallback when the template's slots were empty. Per the user's
        // decision they are gone: an empty template starts with no items,
        // and the user adds them himself (step 3) or writes the whole
        // text (step 4).
        var initial = ReportWizardLogic.InitialDescriptionItems(new List<string> { "", "" });
        Assert.Empty(initial);

        var fromTemplate = ReportWizardLogic.InitialDescriptionItems(new List<string> { "متن تمپلیت", "" });
        Assert.Single(fromTemplate);
        Assert.Equal("متن تمپلیت", fromTemplate[0]);
    }
}
