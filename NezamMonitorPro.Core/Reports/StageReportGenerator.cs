using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using NezamMonitorPro.Core.Models;

namespace NezamMonitorPro.Core.Reports;

/// <summary>
/// Business rules for stage report generation.
/// </summary>
public static class StageReportRules
{
    public static readonly HashSet<string> ExcludedStage1Dates = new()
    {
        "1405/04/01",
        "1405/03/29"
    };

    /// <summary>
    /// Check if a case is eligible for a specific stage report.
    /// </summary>
    public static (bool Eligible, string Reason) CheckEligibility(Case c, int stage, DateTime now)
    {
        if (stage < 1 || stage > 4)
            return (false, "مرحله نامعتبر");

        if (string.IsNullOrWhiteSpace(c.CaseNumber))
            return (false, "شماره پرونده خالی");

        if (string.IsNullOrWhiteSpace(c.Owner))
            return (false, "نام مالک خالی");

        return (true, "آماده تولید");
    }

    /// <summary>
    /// Find matching reports for a stage (handles both "مرحله اول" and "مرحله ۱")
    /// </summary>
    public static List<ReportRecord> FindMatchingReports(Case c, int stage)
    {
        var stageNum = ToPersianNum(stage);
        var stageName = StageName(stage);

        return c.Reports
            .Where(r => r.Stage.Contains($"مرحله {stageNum}") ||
                        r.Stage.Contains($"مرحله {stageName}") ||
                        r.Stage.Contains($"مرحله‌{stageNum}") ||
                        r.Stage.Contains($"مرحله‌{stageName}"))
            .ToList();
    }

    /// <summary>
    /// Generate output folder name: "01 - سميرا رجبي - 1404-514"
    /// </summary>
    public static string GenerateFolderName(int rowNumber, Case c)
    {
        var reversedCase = TextNormalizer.ReverseCaseNumber(c.CaseNumber);
        var safeOwner = TextNormalizer.SanitizeFileName(c.Owner);
        var rowNum = rowNumber.ToString("D2"); // 01, 02, ...
        return $"{rowNum} - {safeOwner} - {reversedCase}";
    }

    /// <summary>
    /// Generate report filename: "گزارش مرحله اول - ۱۴۰۵-۰۶-۰۵.docx"
    /// </summary>
    public static string GenerateReportFilename(int stage, DateTime reportDate, bool useLtr = false)
    {
        var stageWord = StageName(stage);
        var persianDate = PersianDateHelper.ToPersianDateFile(reportDate);
        if (useLtr)
            return $"گزارش مرحله {stageWord} - {persianDate} - LTR.docx";
        return $"گزارش مرحله {stageWord} - {persianDate}.docx";
    }

    public static string StageName(int stage) => stage switch
    {
        1 => "اول",
        2 => "دوم",
        3 => "سوم",
        4 => "چهارم",
        _ => stage.ToString()
    };

    public static string ToPersianNum(int n) => n switch
    {
        1 => "۱", 2 => "۲", 3 => "۳", 4 => "۴",
        _ => n.ToString()
    };

    /// <summary>
    /// Validate a template file for required placeholders.
    /// </summary>
    public static TemplateValidationResult ValidateTemplate(string templatePath)
    {
        var result = new TemplateValidationResult { IsValid = true, TemplatePath = templatePath };

        if (!File.Exists(templatePath))
        {
            result.IsValid = false;
            result.Errors.Add("فایل تمپلیت یافت نشد");
            return result;
        }

        try
        {
            using var doc = WordprocessingDocument.Open(templatePath, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body == null)
            {
                result.IsValid = false;
                result.Errors.Add("فایل Word خالی است");
                return result;
            }

            // Get all text from paragraphs
            var allText = string.Join(" ", body.Descendants<Paragraph>().Select(p => p.InnerText));

            // Required placeholders — search for canonical names (trailing spaces tolerated)
            var required = new[] { "کد_نوسازی", "تاریخ_گزارش", "شماره_پرونده", "مالک", "نوع_کاربری" };

            foreach (var placeholder in required)
            {
                if (allText.Contains(placeholder))
                    result.FoundPlaceholders.Add(placeholder);
                else
                    result.MissingPlaceholders.Add(placeholder);
            }

            // Optional placeholders
            var optional = new[] { "عنوان_بلوک", "شماره_پروانه", "تاریخ_صدور_پروانه", "تعداد_بلوک", "نوع_سازه", "گروه_ساختمانی", "تعداد_طبقات", "هماهنگکننده" };

            foreach (var placeholder in optional)
            {
                if (allText.Contains(placeholder))
                    result.FoundPlaceholders.Add(placeholder);
                else
                    result.OptionalMissing.Add(placeholder);
            }

            if (result.MissingPlaceholders.Count > 0)
            {
                result.IsValid = false;
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"خطا در خواندن تمپلیت: {ex.Message}");
        }

        return result;
    }
}

/// <summary>
/// Generates stage-specific Word documents from templates.
/// </summary>
public class StageReportGenerator
{
    private readonly List<string> _templateDirs = new();

    public StageReportGenerator(string templateDir)
    {
        try { Directory.CreateDirectory(templateDir); } catch { }
        AddTemplateDirectory(templateDir);
    }

    /// <summary>
    /// افزودن مسیر دیگر برای جستجوی قالب‌ها؛ مثلا پوشه «templates» نسخه قبلی برنامه
    /// یا پوشه قالب‌های دانلودی. مسیرهای ناموجود بی‌صدا نادیده گرفته می‌شوند.
    /// </summary>
    public void AddTemplateDirectory(string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir)) return;
        try
        {
            var full = Path.GetFullPath(dir);
            if (!Directory.Exists(full)) return;
            if (_templateDirs.Any(d => string.Equals(d, full, StringComparison.OrdinalIgnoreCase))) return;
            _templateDirs.Add(full);
        }
        catch { }
    }

    private string? _signedTemplateDir;

    /// <summary>When true, FindTemplate looks in the signed folder first (P1).</summary>
    public bool UseSignedTemplates { get; set; }

    /// <summary>Folder holding the signed (stamped) templates. Not part of
    /// the normal search list — only consulted when UseSignedTemplates is on.</summary>
    public void SetSignedTemplateDirectory(string? dir)
    {
        _signedTemplateDir = null;
        if (string.IsNullOrWhiteSpace(dir)) return;
        try
        {
            var full = Path.GetFullPath(dir);
            if (Directory.Exists(full)) _signedTemplateDir = full;
        }
        catch { }
    }

    public string? SignedTemplateDirectory => _signedTemplateDir;

    /// <summary>True when the template file lives under the signed folder.</summary>
    public bool IsSignedTemplate(string? path)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(_signedTemplateDir)) return false;
        try
        {
            var full = Path.GetFullPath(path);
            return full.StartsWith(_signedTemplateDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public IReadOnlyList<string> TemplateDirectories => _templateDirs;

    /// <summary>همه فایل‌های docx در مسیرهای داده‌شده (بدون فایل‌های موقت Word).</summary>
    private static IEnumerable<string> EnumerateTemplateFiles(IEnumerable<string> dirs)
    {
        foreach (var dir in dirs.ToList())
        {
            string[] files;
            try { files = Directory.GetFiles(dir, "*.docx", SearchOption.AllDirectories); }
            catch { continue; }

            foreach (var file in files)
            {
                if (Path.GetFileName(file).StartsWith("~$", StringComparison.Ordinal)) continue;
                yield return file;
            }
        }
    }

    /// <summary>
    /// Generate a single stage report for a case.
    /// </summary>
    public ReportGenerationResult GenerateReport(
        Case caseData,
        int stage,
        int rowNumber,
        string outputDir,
        DateTime now,
        string? templatePathOverride = null,
        bool useLtr = false)
    {
        var result = new ReportGenerationResult
        {
            CaseNumber = caseData.CaseNumber,
            Owner = caseData.Owner,
            Stage = stage,
            RowNumber = rowNumber
        };

        try
        {
            // Find template - use override if provided, otherwise auto-detect
            // (رشته پرونده از ناظرین/حق‌الزحمه همان پرونده گرفته می‌شود و به‌عنوان اولویت تطبیق قالب استفاده می‌شود)
            var disciplineHint = caseData.Engineers?
                                     .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.Discipline))?.Discipline
                                 ?? caseData.Fees?.FirstOrDefault()?.Discipline;

            var templatePath = templatePathOverride ?? FindTemplate(stage, disciplineHint);
            if (templatePath == null)
            {
                result.Success = false;
                result.ErrorMessage = "قالب یافت نشد";
                return result;
            }

            // Validate template
            var validation = StageReportRules.ValidateTemplate(templatePath);
            if (!validation.IsValid)
            {
                result.Success = false;
                result.ErrorMessage = $"قالب ناقص: {string.Join(", ", validation.MissingPlaceholders)}";
                return result;
            }

            // Generate folder and filename
            var folderName = StageReportRules.GenerateFolderName(rowNumber, caseData);
            var fileName = StageReportRules.GenerateReportFilename(stage, now, useLtr);
            var folderPath = Path.Combine(outputDir, folderName);
            Directory.CreateDirectory(folderPath);
            var outputPath = Path.Combine(folderPath, fileName);

            // Check for existing file
            if (File.Exists(outputPath))
            {
                // Overwrite existing file
                File.Delete(outputPath);
            }

            // Copy template and replace placeholders
            File.Copy(templatePath, outputPath, false);
            ReplacePlaceholders(outputPath, caseData, rowNumber, now, useLtr);

            // Verify file was created
            if (!File.Exists(outputPath))
            {
                result.Success = false;
                result.ErrorMessage = "فایل خروجی ایجاد نشد";
                return result;
            }

            result.Success = true;
            result.OutputPath = outputPath;
            result.FolderPath = folderPath;
            result.FileName = fileName;
            result.ReportDate = PersianDateHelper.ToPersianDateDigits(now);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"خطا: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Generate reports for multiple cases.
    /// </summary>
    public List<ReportGenerationResult> GenerateBatch(
        IReadOnlyList<(Case Case, int RowNumber)> cases,
        int stage,
        string outputDir,
        DateTime now)
    {
        var results = new List<ReportGenerationResult>();
        foreach (var (c, row) in cases)
        {
            results.Add(GenerateReport(c, stage, row, outputDir, now));
        }
        return results;
    }

    /// <summary>
    /// List available templates in the templates directory.
    /// </summary>
    public List<TemplateInfo> ListTemplates()
    {
        var templates = new List<TemplateInfo>();

        foreach (var file in EnumerateTemplateFiles(_templateDirs))
        {
            var parsed = TemplateNameParser.Parse(file);
            templates.Add(new TemplateInfo
            {
                Path = parsed.Path,
                FileName = parsed.FileName,
                Discipline = parsed.Discipline,
                Stage = parsed.Stage
            });
        }

        return templates;
    }

    /// <summary>
    /// Find template file by stage (and optionally prefer a specific discipline).
    /// </summary>
    public string? FindTemplateForStage(int stage)
    {
        return FindTemplate(stage);
    }

    public string? FindTemplateForStage(int stage, string? discipline)
    {
        return FindTemplate(stage, discipline);
    }

    /// <summary>
    /// یافتن قالب مرحله با تطبیق هوشمند نام فایل/پوشه:
    /// «مکانیک مرحله اول.docx» ≡ «1.docx» (در پوشه مکانیک) ≡ «مرحله ۱ - مکانیک.docx».
    /// هیچ‌گاه قالب مرحله دیگری به‌جای این مرحله برگردانده نمی‌شود.
    /// </summary>
    private string? FindTemplate(int stage, string? discipline = null)
    {
        if (stage <= 0) return null;
        // P1: a signed template for this stage wins when signed generation is on.
        if (UseSignedTemplates && !string.IsNullOrEmpty(_signedTemplateDir))
        {
            var signed = FindTemplateIn(stage, discipline, new[] { _signedTemplateDir });
            if (signed != null) return signed;
        }
        if (_templateDirs.Count == 0) return null;
        return FindTemplateIn(stage, discipline, _templateDirs);
    }

    private static string? FindTemplateIn(int stage, string? discipline, IEnumerable<string> dirs)
    {
        var candidates = EnumerateTemplateFiles(dirs)
            .Select(TemplateNameParser.Parse)
            .Where(c => c.Stage == stage)
            .ToList();

        if (candidates.Count == 0) return null;

        var wanted = TemplateNameParser.DetectDiscipline(discipline);

        // ۱) دقیقا همان رشته
        if (wanted.Length > 0)
        {
            var exact = candidates.Where(c => c.Discipline == wanted).ToList();
            if (exact.Count > 0) return PickBest(exact, wanted);
        }

        // ۲) قالب‌های عمومی (بدون رشته)
        var generic = candidates.Where(c => c.Discipline.Length == 0).ToList();
        if (generic.Count > 0) return PickBest(generic, wanted);

        // ۳) هر قالبی که مرحله درست دارد
        return PickBest(candidates, wanted);
    }

    /// <summary>
    /// انتخاب بهترین گزینه بین چند قالب هم‌مرحله: نام عددی خالص («1.docx») دقیق‌ترین است،
    /// بعد نامی که شماره مرحله در آن صریح آمده، بعد کوتاه‌ترین مسیر.
    /// </summary>
    private static string PickBest(List<TemplateNameParser.ParsedTemplate> list, string wantedDiscipline)
    {
        if (wantedDiscipline.Length > 0)
        {
            var withDiscipline = list.Where(c => c.Discipline == wantedDiscipline).ToList();
            if (withDiscipline.Count > 0) list = withDiscipline;
        }

        var numbered = list.FirstOrDefault(c => c.FileName.Trim().Length > 0 && c.FileName.Trim().All(char.IsDigit));
        if (numbered is not null) return numbered.Path;

        var explicitStage = list.Where(c => TemplateNameParser.Normalize(c.FileName).Contains("مرحله")).ToList();
        if (explicitStage.Count > 0) list = explicitStage;

        return list.OrderBy(c => c.Path.Length).First().Path;
    }

    /// <summary>
    /// Replace all placeholders in the DOCX file.
    /// </summary>
    /// <summary>
    /// Reverse slash-separated components for RTL-aware presentation.
    /// 1405/128 → 128/1405
    /// 1405/03/11 → 11/03/1405
    /// 1405 → 1405 (unchanged)
    /// </summary>
    public static string ReverseSlashComponents(string input)
    {
        if (string.IsNullOrEmpty(input) || !input.Contains('/'))
            return input;
        var parts = input.Split('/');
        Array.Reverse(parts);
        return string.Join("/", parts);
    }

    private void ReplacePlaceholders(string docxPath, Case caseData, int rowNumber, DateTime now, bool useLtr = false)
    {
        using var doc = WordprocessingDocument.Open(docxPath, true);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body == null) return;

        var spec = caseData.Specification;
        var engineers = caseData.Engineers;

        // Persian date for report
        var persianReportDate = PersianDateHelper.ToPersianDateDigits(now);

        // Helper: normalize to canonical ASCII first, then convert to Persian for Word
        Func<string, string> toWord = s => DigitNormalizer.ToPersianDigits(DigitNormalizer.ToAsciiDigits(s ?? ""));

        // Helper: reverse slash-separated components for LTR presentation
        // Example: 1405/128 → 128/1405, 1405/03/11 → 11/03/1405
        Func<string, string> rtlReverse = useLtr
            ? (s => ReverseSlashComponents(s))
            : (s => s);

        var replacements = new Dictionary<string, string>
        {
            // Identifiers — convert to Persian + apply LTR for slash-containing values
            ["«شماره_پرونده»"] = rtlReverse(toWord(caseData.CaseNumber)),
            ["«شماره_سریال»"] = toWord(caseData.Serial),
            ["«کد_نوسازی»"] = toWord(spec?.RenovationCode ?? ""),
            ["«شماره_پروانه»"] = rtlReverse(toWord(spec?.PermitNumber ?? "")),
            ["«تعداد_بلوک»"] = toWord(spec?.BlockCount ?? ""),
            ["«تعداد_طبقات»"] = toWord(spec?.Floors ?? ""),
            ["«تعداد_واحد»"] = toWord(spec?.Units ?? ""),

            // Dates — convert to Persian + apply LTR for slash-containing values
            ["«تاریخ_گزارش»"] = rtlReverse(toWord(persianReportDate)),
            ["«تاریخ_صدور_پروانه»"] = rtlReverse(toWord(spec?.PermitDate ?? "")),
            ["«تاریخ_ترخیص»"] = rtlReverse(toWord(spec?.ReleaseDate ?? "")),
            ["«مساحت_زمین»"] = toWord(spec?.LandArea ?? ""),
            ["«متراژ_پاراف»"] = toWord(spec?.ParafArea ?? ""),

            // Text values — pass through as-is (already Persian text)
            ["«مالک»"] = caseData.Owner,
            ["«همراه»"] = caseData.OwnerMobile,
            ["«مسولیت»"] = caseData.Responsibility,
            ["«نوع_کاربری»"] = spec?.UsageType ?? "",
            ["«گروه_ساختمانی»"] = spec?.BuildingGroup ?? "",
            ["«نوع_سازه»"] = spec?.StructureType ?? "",
            ["«عنوان_بلوک»"] = spec?.BlockTitle ?? "",
            ["«صادرکننده_پروانه»"] = spec?.Issuer ?? "",
            ["«محدوده_طرح»"] = spec?.PlanZone ?? "",
            ["«آدرس»"] = spec?.Address ?? "",
            ["«ناظر_معماری»"] = engineers.FirstOrDefault(e => e.Discipline.Contains("معمار"))?.Name ?? "",
            ["«ناظر_عمران»"] = engineers.FirstOrDefault(e => e.Discipline.Contains("عمران"))?.Name ?? "",
            ["«ناظر_مکانیک»"] = engineers.FirstOrDefault(e => e.Discipline.Contains("مکانیک") || e.Discipline.Contains("مكانيك"))?.Name ?? "",
            ["«ناظر_برق»"] = engineers.FirstOrDefault(e => e.Discipline.Contains("برق"))?.Name ?? "",
            ["«هماهنگکننده»"] = engineers.FirstOrDefault(e => e.Discipline.Contains("هماهنگ"))?.Name ?? "",
            // فیلدهای جدید: شماره تماس و صلاحیت ناظرین
            ["«تلفن_ناظر_معماری»"] = engineers.FirstOrDefault(e => e.Discipline.Contains("معمار"))?.Phone ?? "",
            ["«تلفن_ناظر_عمران»"] = engineers.FirstOrDefault(e => e.Discipline.Contains("عمران"))?.Phone ?? "",
            ["«تلفن_ناظر_مکانیک»"] = engineers.FirstOrDefault(e => e.Discipline.Contains("مکانیک") || e.Discipline.Contains("مكانيك"))?.Phone ?? "",
            ["«تلفن_ناظر_برق»"] = engineers.FirstOrDefault(e => e.Discipline.Contains("برق"))?.Phone ?? "",
            ["«صلاحیت_ناظر_معماری»"] = engineers.FirstOrDefault(e => e.Discipline.Contains("معمار"))?.SupervisionLevel ?? "",
            ["«صلاحیت_ناظر_عمران»"] = engineers.FirstOrDefault(e => e.Discipline.Contains("عمران"))?.SupervisionLevel ?? "",
            ["«صلاحیت_ناظر_مکانیک»"] = engineers.FirstOrDefault(e => e.Discipline.Contains("مکانیک") || e.Discipline.Contains("مكانيك"))?.SupervisionLevel ?? "",
            ["«صلاحیت_ناظر_برق»"] = engineers.FirstOrDefault(e => e.Discipline.Contains("برق"))?.SupervisionLevel ?? "",
        };

        // Also add trailing-space variants (Word may add spaces)
        var extraReplacements = new Dictionary<string, string>();
        foreach (var kv in replacements)
        {
            var trimmed = kv.Key.TrimEnd();
            if (trimmed != kv.Key && !replacements.ContainsKey(trimmed))
                extraReplacements[trimmed] = kv.Value;
        }
        foreach (var kv in extraReplacements)
            replacements[kv.Key] = kv.Value;

        // Replace in all paragraphs (handles split runs)
        foreach (var para in body.Descendants<Paragraph>())
            ReplaceInParagraph(para, replacements);

        // Replace in tables
        foreach (var table in body.Descendants<Table>())
            foreach (var para in table.Descendants<Paragraph>())
                ReplaceInParagraph(para, replacements);

        doc.MainDocumentPart?.Document?.Save();
    }

    /// <summary>
    /// Replace variables in a paragraph, span-aware: a placeholder is replaced
    /// only inside the run(s) it spans; every other run is left byte-identical.
    /// Mirrors the Android implementation (3.8.1, ReportGeneratorScreen.kt):
    /// runs are never added or removed, only runs whose text changed are written
    /// back, and runs that received replacement text get the B Nazanin font.
    /// </summary>
    private static void ReplaceInParagraph(Paragraph para, Dictionary<string, string> replacements)
    {
        var runs = para.Descendants<Run>().ToList();
        if (runs.Count == 0) return;

        // The text of each run, from its &lt;w:t&gt; elements only (a run can also
        // hold breaks/tabs/drawings, which are not text and must not shift offsets).
        var originalTexts = runs.Select(r => string.Concat(r.Elements<Text>().Select(t => t.Text ?? ""))).ToList();
        var runTexts = originalTexts.ToList();
        // Which runs received replacement text (their font becomes B Nazanin).
        var touched = new bool[runs.Count];
        var changed = false;

        foreach (var (key, value) in replacements)
        {
            // Variants Word can produce: the key itself, the key with a space
            // before the closing guillemet, and the key with a space after the
            // opening guillemet. (Android tries the first two; the third keeps
            // the coverage the old normalization had.)
            foreach (var needle in new[] { key, key.Replace("»", " »"), key.Replace("«", "« ") })
            {
                if (string.IsNullOrEmpty(needle)) continue;
                // All non-overlapping occurrences in the current text, then apply
                // them right-to-left so earlier offsets stay valid.
                var full = string.Concat(runTexts);
                var occurrences = new List<int>();
                var idx = full.IndexOf(needle, StringComparison.Ordinal);
                while (idx >= 0)
                {
                    occurrences.Add(idx);
                    idx = full.IndexOf(needle, idx + needle.Length, StringComparison.Ordinal);
                }
                for (var o = occurrences.Count - 1; o >= 0; o--)
                {
                    var occ = occurrences[o];
                    var occEnd = occ + needle.Length;
                    var pos = 0;
                    for (var i = 0; i < runTexts.Count; i++)
                    {
                        var runStart = pos;
                        var runEnd = pos + runTexts[i].Length;
                        pos = runEnd;
                        if (runEnd <= occ || runStart >= occEnd) continue;
                        if (runStart <= occ && occ < runEnd)
                        {
                            // The run the placeholder starts in: it keeps the
                            // replacement text (plus whatever of this run lies
                            // after the placeholder, when it ends inside it).
                            var relStart = occ - runStart;
                            var relEnd = Math.Min(occEnd - runStart, runTexts[i].Length);
                            runTexts[i] = runTexts[i].Substring(0, relStart) + value + runTexts[i].Substring(relEnd);
                            touched[i] = true;
                        }
                        else if (runStart >= occ && runEnd <= occEnd)
                        {
                            // A run fully inside the placeholder span: emptied.
                            runTexts[i] = "";
                        }
                        else
                        {
                            // A run the placeholder ends in: drop its covered prefix.
                            runTexts[i] = runTexts[i].Substring(occEnd - runStart);
                        }
                    }
                    changed = true;
                }
            }
        }

        if (!changed) return;

        // Write back only the runs whose text changed; untouched runs are never
        // written, so their properties cannot change.
        for (var i = 0; i < runs.Count; i++)
        {
            if (runTexts[i] != originalTexts[i])
            {
                SetRunText(runs[i], runTexts[i]);
                if (touched[i]) SetRunFontBNazanin(runs[i]);
            }
        }
    }

    /// <summary>
    /// Replace a run's text, keeping its properties: the first &lt;w:t&gt; gets the
    /// new text and any extra &lt;w:t&gt; elements are removed (a run with several
    /// text elements would otherwise keep stale text after the first one).
    /// Breaks/tabs/drawings in the run are untouched (only &lt;w:t&gt; is edited).
    /// </summary>
    private static void SetRunText(Run run, string text)
    {
        var textElements = run.Elements<Text>().ToList();
        if (textElements.Count == 0) return;
        textElements[0].Text = text;
        if (text.Length > 0 && (char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[text.Length - 1])))
            textElements[0].Space = SpaceProcessingModeValues.Preserve;
        for (var i = 1; i < textElements.Count; i++)
            textElements[i].Remove();
    }

    /// <summary>
    /// Set a run's font to B Nazanin (ascii + hAnsi + cs, unconditionally).
    /// </summary>
    private static void SetRunFontBNazanin(Run run)
    {
        var rPr = run.RunProperties ?? run.PrependChild(new RunProperties());
        var rFonts = rPr.GetFirstChild<RunFonts>();
        if (rFonts == null)
        {
            rFonts = new RunFonts();
            // rFonts precedes b/i/color/sz in CT_RPr: insert after rStyle if
            // there is one, otherwise at the front.
            var rStyle = rPr.GetFirstChild<RunStyle>();
            if (rStyle != null) rPr.InsertAfter(rFonts, rStyle);
            else rPr.PrependChild(rFonts);
        }
        rFonts.Ascii = "B Nazanin";
        rFonts.HighAnsi = "B Nazanin";
        rFonts.ComplexScript = "B Nazanin";
    }
}

/// <summary>
/// Template validation result.
/// </summary>
public sealed class TemplateValidationResult
{
    public bool IsValid { get; set; }
    public string TemplatePath { get; set; } = "";
    public List<string> FoundPlaceholders { get; set; } = new();
    public List<string> MissingPlaceholders { get; set; } = new();
    public List<string> OptionalMissing { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Template information.
/// </summary>
public sealed class TemplateInfo
{
    public string Path { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Discipline { get; set; } = "";
    public int Stage { get; set; }
}

/// <summary>
/// Result of a single report generation.
/// </summary>
public sealed class ReportGenerationResult
{
    public string CaseNumber { get; set; } = "";
    public string Owner { get; set; } = "";
    public int Stage { get; set; }
    public int RowNumber { get; set; }
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = "";
    public string? OutputPath { get; set; }
    public string? FolderPath { get; set; }
    public string? FileName { get; set; }
    public string ReportDate { get; set; } = "";
}
