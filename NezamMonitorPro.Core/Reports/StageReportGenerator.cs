using System.Text.RegularExpressions;
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
    /// افزودن مسیر دیگر برای جستجوی قالب‌ها؛ مثلاً پوشهٔ «templates» نسخهٔ قبلی برنامه
    /// یا پوشهٔ قالب‌های دانلودی. مسیرهای ناموجود بی‌صدا نادیده گرفته می‌شوند.
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

    public IReadOnlyList<string> TemplateDirectories => _templateDirs;

    /// <summary>همهٔ فایل‌های docx در همهٔ مسیرهای قالب (بدون فایل‌های موقت Word).</summary>
    private IEnumerable<string> EnumerateTemplateFiles()
    {
        foreach (var dir in _templateDirs.ToList())
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
            // (رشتهٔ پرونده از ناظرین/حق‌الزحمهٔ همان پرونده گرفته می‌شود و به‌عنوان اولویت تطبیق قالب استفاده می‌شود)
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

        foreach (var file in EnumerateTemplateFiles())
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
    /// «مکانیک مرحله اول.docx» ≡ «1.docx» (در پوشهٔ مکانیک) ≡ «مرحله ۱ - مکانیک.docx».
    /// هیچ‌گاه قالب مرحلهٔ دیگری به‌جای این مرحله برگردانده نمی‌شود.
    /// </summary>
    private string? FindTemplate(int stage, string? discipline = null)
    {
        if (stage <= 0) return null;
        if (_templateDirs.Count == 0) return null;

        var candidates = EnumerateTemplateFiles()
            .Select(TemplateNameParser.Parse)
            .Where(c => c.Stage == stage)
            .ToList();

        if (candidates.Count == 0) return null;

        var wanted = TemplateNameParser.DetectDiscipline(discipline);

        // ۱) دقیقاً همان رشته
        if (wanted.Length > 0)
        {
            var exact = candidates.Where(c => c.Discipline == wanted).ToList();
            if (exact.Count > 0) return PickBest(exact, wanted);
        }

        // ۲) قالب‌های عمومی (بدون رشته)
        var generic = candidates.Where(c => c.Discipline.Length == 0).ToList();
        if (generic.Count > 0) return PickBest(generic, wanted);

        // ۳) هر قالبی که مرحلهٔ درست دارد
        return PickBest(candidates, wanted);
    }

    /// <summary>
    /// انتخاب بهترین گزینه بین چند قالب هم‌مرحله: نام عددی خالص («1.docx») دقیق‌ترین است،
    /// بعد نامی که شمارهٔ مرحله در آن صریح آمده، بعد کوتاه‌ترین مسیر.
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
            ["«مسئولیت»"] = caseData.Responsibility,
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
    /// Replace variables in a paragraph, handling split runs by merging text first.
    /// </summary>
    private static void ReplaceInParagraph(Paragraph para, Dictionary<string, string> replacements)
    {
        var runs = para.Descendants<Run>().ToList();
        if (runs.Count == 0) return;

        // Concatenate all run texts
        var fullText = string.Concat(runs.Select(r => r.InnerText));

        // Normalize: collapse multiple spaces between « and » to single space
        // This handles Word adding extra spaces inside placeholders
        var normalized = Regex.Replace(fullText, @"«\s+", "«");
        normalized = Regex.Replace(normalized, @"\s+»", "»");

        // Check if any variable exists (check both original and normalized)
        bool hasVariable = replacements.Keys.Any(k => fullText.Contains(k) || normalized.Contains(k));
        if (!hasVariable) return;

        // Replace in normalized text
        foreach (var (key, value) in replacements)
        {
            // Also try with normalized key
            var normKey = Regex.Replace(key, @"«\s+", "«");
            normKey = Regex.Replace(normKey, @"\s+»", "»");
            
            normalized = normalized.Replace(normKey, value);
        }

        // Put all text in the first run, clear the rest
        var firstRun = runs[0];
        var textEl = firstRun.GetFirstChild<Text>();
        if (textEl != null)
            textEl.Text = normalized;
        else
            firstRun.AppendChild(new Text(fullText));

        for (int i = 1; i < runs.Count; i++)
        {
            var t = runs[i].GetFirstChild<Text>();
            if (t != null) t.Text = "";
        }
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
