using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using NezamMonitorPro.Core.Models;

namespace NezamMonitorPro.Core.Reports;

// ==================== REPORT WIZARD — PURE DOCX LOGIC (v12.6) ====================
// Windows port of the Android wizard logic (ReportWizardLogic.kt, v3.11.0–
// v3.11.2). No WPF dependency: this file only uses OpenXml + Core, so the
// exact code that runs in the app is what the tests and the harness prove.
// The template facts it relies on were dumped from the real templates
// (1–4.docx):
//  - Table 1 is the checkbox table: header «ردیف | شرح کنترل | انجام شده |
//    انجام نشده | شامل نمی‌شود», data rows «N | <description> | ☐ | ☐ | ☐»,
//    each ☐ a single run in "Segoe UI Symbol" with no explicit color.
//  - Table 2 is توضیحات: one cell, paragraphs «توضیحات:», intro, «۱- »,
//    «۲- », closing. The numbered items are EMPTY in 1–4.docx.
//  - Fully content-driven (v3.11.2): NO header words and NO fixed columns
//    are assumed. The checkbox table is the table with ☐ rows; the header
//    row is the row above the first ☐ row and its cells at the ☐ columns
//    are the status labels; the checkbox columns are the cells with ☐,
//    the row-number column is the cell that is only digits, and the
//    description column is the first cell that is neither. The توضیحات
//    cell is the cell with numbered («N- ») paragraphs — the «توضیحات»
//    title is only a hint. A future template may rename its columns,
//    reorder them, add a fourth status column, or rename the section,
//    and the wizard still reads it as-is; the only fallback is the
//    default three status labels when a file has no readable header.
//  - The only checkbox mark in the templates is ☐ (no ☑, no w14:checkbox,
//    no FORMCHECKBOX). Marking = replacing ☐ with ☑ in the same run, which
//    is what Word shows as ☑ and what the PDF renderer turns into a
//    stroked black square with a black tick.

/// <summary>What the wizard collected (field edits, checkbox picks,
/// توضیحات items) to apply on top of the normal generation.</summary>
public sealed class ReportWizardOverrides
{
    /// <summary>Stage-1 field edits, keyed by the placeholder WITH
    /// guillemets («کد_نوسازی»). Only keys the user actually edited.</summary>
    public Dictionary<string, string> FieldValues { get; set; } = new();

    /// <summary>Stage-2 picks: row index (0-based among parsed rows) →
    /// status-column index (0-based among the file's own status columns).
    /// Radio rule: at most one column per row.</summary>
    public Dictionary<int, int> CheckboxSelections { get; set; } = new();

    /// <summary>Stage-3 items, or null when the template has no توضیحات
    /// table — generation then leaves the template's توضیحات untouched.</summary>
    public List<string>? DescriptionItems { get; set; }

    /// <summary>Stage-4 full text (the whole توضیحات cell, one line per
    /// paragraph), or null when the user did not edit it. When set, it
    /// wins over DescriptionItems: the cell is rewritten from this text
    /// instead of from the numbered rows.</summary>
    public string? DescriptionFullText { get; set; }
}

public static class ReportWizardLogic
{
    public const char CheckboxEmpty = '☐';
    public const char CheckboxMarked = '☑';

    /// <summary>The status-column labels, used ONLY as a fallback when the
    /// template file has no readable header row. The real labels are read
    /// from the file's header row — see WizardCheckboxTable.Headers — so a
    /// future template may rename its columns or add a fourth one and the
    /// wizard still shows the file's own words.</summary>
    public static readonly IReadOnlyList<string> FallbackCheckboxColumns =
        new[] { "انجام شده", "انجام نشده", "شامل نمی‌شود" };

    /// <summary>One row of the checkbox table (Index = 0-based position
    /// among the parsed data rows; TableRow = the raw row index in the
    /// table, used for marking, so filtered-out rows cannot shift the
    /// mapping).</summary>
    public sealed record WizardCheckboxRow(int Index, string Number, string Description, int TableRow);

    /// <summary>The parsed checkbox table. Headers are the status-column
    /// labels read from the file's header row (or the fallback labels when
    /// the file has no readable header) — one label per status column, in
    /// order. CheckboxCols are the raw column indices of the status
    /// columns and DescriptionCol the raw index of the description column.
    /// DataStartRow is the raw table row the data rows start at (header
    /// row + 1, or the first ☐ row when there is no header).</summary>
    public sealed record WizardCheckboxTable(
        IReadOnlyList<string> Headers,
        IReadOnlyList<WizardCheckboxRow> Rows,
        int DataStartRow = 1,
        IReadOnlyList<int>? CheckboxCols = null,
        int DescriptionCol = 1)
    {
        public IReadOnlyList<int> CheckboxColumns { get; init; } = CheckboxCols ?? Array.Empty<int>();
    }

    /// <summary>What the wizard read from the template file. Either table
    /// part may be absent when the template has no such table — the wizard
    /// then skips that stage instead of inventing rows. PresentFieldKeys
    /// are the editable fields whose placeholder the template actually
    /// contains: only those are shown in the wizard, because a field the
    /// template does not have cannot change the report.
    /// DescriptionPrefixLines / DescriptionSuffixLines are the توضیحات
    /// cell's paragraphs before the first numbered item (title + intro)
    /// and after the last one (closing) — step 4 shows the whole text
    /// (prefix + numbered items + suffix) for editing.</summary>
    public sealed record WizardTemplateData(
        WizardCheckboxTable? CheckboxTable,
        IReadOnlyList<string>? DescriptionItems,
        IReadOnlyList<string> PresentFieldKeys,
        IReadOnlyList<string> DescriptionPrefixLines,
        IReadOnlyList<string> DescriptionSuffixLines);

    /// <summary>Initial توضیحات items for the wizard: exactly the
    /// template's own numbered items (empty slots dropped). Nothing is
    /// invented here — when the template's slots are all empty, the
    /// wizard starts with no items and the user adds them himself
    /// (step 3) or writes the whole text (step 4).</summary>
    public static List<string> InitialDescriptionItems(IReadOnlyList<string>? templateItems)
    {
        return templateItems?.Select(s => s.Trim()).Where(s => s.Length > 0).ToList()
               ?? new List<string>();
    }

    // ---------- Stage-1 editable fields ----------

    /// <summary>The placeholders the wizard lets the user edit, in display
    /// order: the 14 Android fields first, then the Windows-only ones
    /// (the prompt lists them as 13, but enumerates 18: 10 case fields +
    /// 4 supervisor phones + 4 supervisor levels — the enumeration wins).
    /// The four «ناظر_معماری/عمران/مکانیک/برق» names stay auto-only, like
    /// in Android: they are filled by the normal generation and never
    /// edited in the wizard.</summary>
    public static readonly IReadOnlyList<string> EditableFieldKeys =
        new[]
        {
            "«کد_نوسازی»", "«تاریخ_گزارش»", "«شماره_پرونده»", "«عنوان_بلوک»",
            "«شماره_پروانه»", "«تاریخ_صدور_پروانه»", "«تعداد_بلوک»", "«نوع_کاربری»",
            "«نوع_سازه»", "«گروه_ساختمانی»", "«تعداد_طبقات»", "«مالک»",
            "«نام ناظر»", "«هماهنگکننده»",
            "«شماره_سریال»", "«تعداد_واحد»", "«تاریخ_ترخیص»", "«مساحت_زمین»",
            "«متراژ_پاراف»", "«همراه»", "«مسولیت»", "«صادرکننده_پروانه»",
            "«محدوده_طرح»", "«آدرس»",
            "«تلفن_ناظر_معماری»", "«تلفن_ناظر_عمران»", "«تلفن_ناظر_مکانیک»", "«تلفن_ناظر_برق»",
            "«صلاحیت_ناظر_معماری»", "«صلاحیت_ناظر_عمران»", "«صلاحیت_ناظر_مکانیک»", "«صلاحیت_ناظر_برق»",
        };

    /// <summary>Persian display label for an editable placeholder key
    /// («کد_نوسازی» → «کد نوسازی»). The «مسولیت» key keeps its v12.1
    /// spelling: the ئ is not used in the app's UI text, because B
    /// Nazanin renders it as a tofu box.</summary>
    public static string FieldLabel(string key)
    {
        if (key == "«نام ناظر»") return "مهندس ناظر";
        if (key == "«هماهنگکننده»") return "هماهنگ‌کننده";
        return key.Trim('«', '»', '"').Replace("_", " ");
    }

    /// <summary>A value as the wizard shows it (and as an edited value is
    /// written): Arabic letters unified to Persian (ي→ی، ك→ک) and Arabic
    /// diacritics/tatweel stripped — B Nazanin has no glyphs for them, so
    /// they show as squares in the PDF. The v12.1 source sweep applies
    /// here too (ة→ه، ء deleted, أ→ا، ؤ→و، ئ→ی): the user instructed
    /// those characters not to be used at all, because B Nazanin renders
    /// them as tofu boxes. Unedited values keep exactly what the normal
    /// generation writes.</summary>
    public static string ToPersianDisplay(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var unified = TextNormalizer.UnifyPersianChars(value);
        var sb = new System.Text.StringBuilder(unified.Length);
        foreach (var ch in unified)
        {
            if ((ch >= '\u064B' && ch <= '\u0652') || ch == '\u0654' || ch == '\u0655' || ch == '\u0670' || ch == '\u0640') continue;
            if (ch == '\u0621') continue; // ء deleted
            if (ch == '\u0629') { sb.Append('\u0647'); continue; } // ة -> ه
            if (ch == '\u0623') { sb.Append('\u0627'); continue; } // أ -> ا
            if (ch == '\u0624') { sb.Append('\u0648'); continue; } // ؤ -> و
            if (ch == '\u0626') { sb.Append('\u06CC'); continue; } // ئ -> ی
            sb.Append(ch);
        }
        return sb.ToString();
    }

    // ---------- Stage-count / suggestion rule ----------

    /// <summary>How many report stages a case needs, from its building
    /// group + discipline — the Android rule (ReportSchedule.kt):
    /// group الف → 3; شهرسازی → 4 only for groups ج/د, else 3;
    /// عمران/معماری/برق/مکانیک/نقشه‌برداری → 4; anything else → 3.
    /// The Windows generator's own rule (GetMaxStageForGroup) is
    /// group-only and is NOT changed; this one is only the wizard's
    /// stage picker + suggestion.</summary>
    public static int StageCountForCase(string? buildingGroup, string? discipline)
    {
        var group = NormalizeGroup(buildingGroup);
        if (group.Length == 0) return 3;
        var disc = NormalizeDiscipline(discipline);
        var fourStageDisciplines = new HashSet<string>
            { "عمران", "معماری", "برق", "مکانیک", "نقشهبرداری" };
        if (group == "الف") return 3;
        if (disc == "شهرسازی") return (group == "ج" || group == "د") ? 4 : 3;
        if (fourStageDisciplines.Contains(disc)) return 4;
        return 3;
    }

    /// <summary>The suggested stage: the nearest stage in 1..stageCount
    /// that is not registered yet for this case, or 0 when every stage
    /// is registered (the user may still pick any stage).</summary>
    public static int SuggestedStage(int stageCount, IReadOnlyCollection<int> registeredStages)
    {
        for (var stage = 1; stage <= stageCount; stage++)
            if (!registeredStages.Contains(stage)) return stage;
        return 0;
    }

    private static string NormalizeGroup(string? group)
    {
        var g = (group ?? "").Trim();
        if (g.StartsWith("الف")) return "الف";
        if (g.StartsWith("ب")) return "ب";
        if (g.StartsWith("ج")) return "ج";
        if (g.StartsWith("د")) return "د";
        return "";
    }

    private static string NormalizeDiscipline(string? discipline)
    {
        return TextNormalizer.UnifyPersianChars(discipline ?? "")
            .Trim().Replace("\u200C", "").Replace(" ", "");
    }

    // ---------- Checkbox table ----------

    /// <summary>A cell's text is a row number when it is only digits
    /// (Persian, Arabic or Latin) — «۱», «2», «٠٣».</summary>
    private static bool IsNumberCell(string? text)
    {
        var t = (text ?? "").Trim();
        return t.Length > 0 && t.All(ch =>
            (ch >= '0' && ch <= '9') || (ch >= '۰' && ch <= '۹') || (ch >= '٠' && ch <= '٩'));
    }

    /// <summary>A checkbox cell holds an empty box or a marked one — after
    /// marking, a cell holds ☑ instead of ☐, and the column detection must
    /// still see it (otherwise counting would miss the very marks it is
    /// counting).</summary>
    private static bool IsCheckboxCell(string? text)
    {
        var t = text ?? "";
        return t.Contains(CheckboxEmpty) || t.Contains(CheckboxMarked);
    }

    private static string CellText(TableCell cell) => cell.InnerText ?? "";

    /// <summary>Find the checkbox table: the table with data rows carrying
    /// checkbox cells (☐, or ☑ once marked). Nothing about the header words
    /// is assumed. The header row is the row right above the first ☐ row
    /// (when there is one), and the status labels are read from it; when
    /// there is no header row the fallback labels are used. Columns are
    /// found by CONTENT, not by index: the checkbox columns are the cells
    /// with ☐, the row-number column is the cell that is only digits, and
    /// the description column is the first cell that is neither a checkbox
    /// cell nor the number cell. Returns the table and its parsed rows, or
    /// null when no table has ☐ rows.</summary>
    public static (Table Table, WizardCheckboxTable Parsed)? FindCheckboxTable(WordprocessingDocument doc)
    {
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body == null) return null;
        foreach (var table in body.Elements<Table>())
        {
            var rows = table.Elements<TableRow>().ToList();
            if (rows.Count == 0) continue;
            // First data row: the first row with checkbox cells (☐ or ☑)
            // in at least two cells (one box could be a stray mark; a
            // checkbox row has several).
            var firstBoxRow = rows.FindIndex(row =>
                row.Elements<TableCell>().Count(c => IsCheckboxCell(CellText(c))) >= 2);
            if (firstBoxRow < 0) continue;
            // The checkbox columns: the checkbox cells of the first data row.
            var firstCells = rows[firstBoxRow].Elements<TableCell>().ToList();
            var checkboxCols = firstCells
                .Select((cell, i) => (cell, i))
                .Where(x => IsCheckboxCell(CellText(x.cell)))
                .Select(x => x.i).ToList();
            if (checkboxCols.Count == 0) continue;
            // The header row is the row right above the first data row; its
            // cells at the checkbox columns are the status labels. A missing
            // or blank label falls back to the default label for that
            // position (and then to a generic «ستون N»).
            var headerRow = firstBoxRow > 0 ? rows[firstBoxRow - 1] : null;
            var headerCells = headerRow?.Elements<TableCell>().ToList();
            var headers = checkboxCols.Select((col, i) =>
            {
                var label = headerCells != null && col < headerCells.Count
                    ? headerCells[col].InnerText?.Trim() : null;
                if (!string.IsNullOrEmpty(label)) return label!;
                if (i < FallbackCheckboxColumns.Count) return FallbackCheckboxColumns[i];
                return $"ستون {i + 1}";
            }).ToList();
            // The description column: the first cell of the first data row
            // that is neither a checkbox cell nor the row-number cell.
            var numberCol = firstCells.FindIndex(c => IsNumberCell(CellText(c)));
            var descriptionCol = firstCells
                .Select((cell, i) => i)
                .FirstOrDefault(i => !checkboxCols.Contains(i) && i != numberCol
                    && !string.IsNullOrWhiteSpace(CellText(firstCells[i])), 1);
            // Data rows: rows from the first ☐ row on that still carry a
            // checkbox cell (☐ or ☑) in a checkbox column (a trailing
            // non-checkbox row in the same table is not a checkbox row).
            var parsedRows = new List<WizardCheckboxRow>();
            for (var r = firstBoxRow; r < rows.Count; r++)
            {
                var cells = rows[r].Elements<TableCell>().ToList();
                var hasBox = checkboxCols.Any(c =>
                    c < cells.Count && IsCheckboxCell(CellText(cells[c])));
                if (!hasBox) continue;
                var number = numberCol >= 0 && numberCol < cells.Count
                    ? CellText(cells[numberCol]).Trim() : "";
                var description = descriptionCol < cells.Count
                    ? CellText(cells[descriptionCol]).Trim() : "";
                parsedRows.Add(new WizardCheckboxRow(parsedRows.Count, number, description, r));
            }
            if (parsedRows.Count == 0) continue;
            return (table, new WizardCheckboxTable(headers, parsedRows, firstBoxRow, checkboxCols, descriptionCol));
        }
        return null;
    }

    /// <summary>The one-checkbox-per-row rule (radio behavior): selecting a
    /// column in a row replaces any previous selection in that row; tapping
    /// the already selected column clears it (no checkbox selected in that
    /// row).</summary>
    public static Dictionary<int, int> SelectCheckbox(
        IReadOnlyDictionary<int, int> current, int row, int col)
    {
        var next = new Dictionary<int, int>(current);
        if (next.TryGetValue(row, out var existing) && existing == col) next.Remove(row);
        else next[row] = col;
        return next;
    }

    /// <summary>Mark the selected cells in the document: replace ☐ with ☑
    /// in the chosen column's cell of each selected row, and set the run
    /// color to explicit black (000000) so the tick is black in Word too
    /// (the template runs have no explicit color). Returns how many cells
    /// were actually marked. Cells without a ☐ are left alone; unknown
    /// rows/columns are ignored. The column is the status column's
    /// position among the file's own status columns, so a fourth status
    /// column marks exactly like the first three.</summary>
    public static int ApplyCheckboxSelections(WordprocessingDocument doc, IReadOnlyDictionary<int, int> selections)
    {
        var found = FindCheckboxTable(doc);
        if (found == null) return 0;
        var (table, parsed) = found.Value;
        var rows = table.Elements<TableRow>().ToList();
        var marked = 0;
        foreach (var (rowIdx, colIdx) in selections)
        {
            var parsedRow = parsed.Rows.FirstOrDefault(r => r.Index == rowIdx);
            if (parsedRow == null) continue;
            if (colIdx < 0 || colIdx >= parsed.CheckboxColumns.Count) continue;
            var checkboxCol = parsed.CheckboxColumns[colIdx];
            if (parsedRow.TableRow < 0 || parsedRow.TableRow >= rows.Count) continue;
            var cells = rows[parsedRow.TableRow].Elements<TableCell>().ToList();
            if (checkboxCol < 0 || checkboxCol >= cells.Count) continue;
            var cellMarked = false;
            foreach (var para in cells[checkboxCol].Descendants<Paragraph>())
            {
                foreach (var run in para.Descendants<Run>())
                {
                    var text = RunText(run);
                    if (!text.Contains(CheckboxEmpty)) continue;
                    SetRunText(run, text.Replace(CheckboxEmpty, CheckboxMarked));
                    SetRunColorBlack(run);
                    cellMarked = true;
                }
            }
            if (cellMarked) marked++;
        }
        return marked;
    }

    /// <summary>How many ☑ marks the checkbox table currently holds (0 when
    /// there is no checkbox table). Used by the wizard to verify the marks
    /// landed in the generated file.</summary>
    public static int CountMarkedCheckboxes(WordprocessingDocument doc)
    {
        var found = FindCheckboxTable(doc);
        if (found == null) return 0;
        var (table, parsed) = found.Value;
        var rows = table.Elements<TableRow>().ToList();
        var count = 0;
        foreach (var parsedRow in parsed.Rows)
        {
            if (parsedRow.TableRow < 0 || parsedRow.TableRow >= rows.Count) continue;
            var cells = rows[parsedRow.TableRow].Elements<TableCell>().ToList();
            foreach (var checkboxCol in parsed.CheckboxColumns)
            {
                if (checkboxCol < 0 || checkboxCol >= cells.Count) continue;
                foreach (var para in cells[checkboxCol].Descendants<Paragraph>())
                    foreach (var run in para.Descendants<Run>())
                        if (RunText(run).Contains(CheckboxMarked)) count++;
            }
        }
        return count;
    }

    // ---------- توضیحات table ----------

    /// <summary>A numbered توضیحات paragraph: a line starting with a
    /// (Persian, Arabic or Latin) number, a hyphen and optional space —
    /// e.g. «۱- », «2- ».</summary>
    // A numbered توضیحات item starts with digits, a dash, then a space
    // or the end of the text («۱- », «2- »). The space/end after the
    // dash is essential: without it, a serial like «109855-2032000»
    // (digits-dash-digits, no space) would match and the wrong cell
    // would be picked as the توضیحات cell.
    private static readonly Regex DescriptionNumberRegex =
        new(@"^[۰-۹٠-٩0-9]+[-–—](\s|$)", RegexOptions.Compiled);

    /// <summary>The توضیحات cell: a cell of any table whose paragraphs
    /// include numbered items («۱- », «2- » …). The «توضیحات» title is only
    /// a hint, not a requirement — a future template may rename the section
    /// and the numbered items still identify it. Preference order: a cell
    /// with both the title and numbered items, then a cell with numbered
    /// items only, then (for the old behaviour) a cell with the title only.
    /// Null when no cell has any of these.</summary>
    public static TableCell? FindDescriptionCell(WordprocessingDocument doc)
    {
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body == null) return null;
        var cells = body.Descendants<TableCell>().ToList();
        static bool HasTitle(TableCell cell) =>
            cell.Elements<Paragraph>().Any(p => (p.InnerText ?? "").Trim().StartsWith("توضیحات"));
        static bool HasNumbered(TableCell cell) =>
            cell.Elements<Paragraph>().Any(p => DescriptionNumberRegex.IsMatch((p.InnerText ?? "").Trim()));
        return cells.FirstOrDefault(c => HasTitle(c) && HasNumbered(c))
            ?? cells.FirstOrDefault(c => HasNumbered(c))
            ?? cells.FirstOrDefault(c => HasTitle(c));
    }

    /// <summary>Split the description cell's paragraphs into the lines
    /// before the first numbered item (title + intro), the numbered items
    /// themselves, and the lines after the last numbered item (closing).
    /// Null when the template has no توضیحات table. When the cell has no
    /// numbered paragraph at all, every paragraph is a prefix line.</summary>
    public static (IReadOnlyList<string> Prefix, List<string> Items, IReadOnlyList<string> Suffix)? SplitDescriptionCell(WordprocessingDocument doc)
    {
        var cell = FindDescriptionCell(doc);
        if (cell == null) return null;
        var paras = cell.Elements<Paragraph>().Select(p => (p.InnerText ?? "").Trim()).ToList();
        var numbered = paras.Select((t, i) => (t, i))
            .Where(x => DescriptionNumberRegex.IsMatch(x.t)).Select(x => x.i).ToList();
        if (numbered.Count == 0) return (paras, new List<string>(), new List<string>());
        var first = numbered[0];
        var last = numbered[^1];
        var prefix = paras.Take(first).ToList();
        var items = numbered.Select(i =>
        {
            var match = DescriptionNumberRegex.Match(paras[i]);
            return paras[i].Substring(match.Length).Trim();
        }).ToList();
        var suffix = paras.Skip(last + 1).ToList();
        return (prefix, items, suffix);
    }

    /// <summary>The numbered توضیحات items of the template, in order (empty
    /// string for an empty item). Null when the template has no توضیحات
    /// table.</summary>
    public static List<string>? ParseDescriptionItems(WordprocessingDocument doc)
    {
        return SplitDescriptionCell(doc)?.Items;
    }

    /// <summary>The whole توضیحات text as the report will show it: the
    /// title + intro lines, the numbered items («۱- …» with Persian
    /// digits), then the closing lines — one line each, joined with
    /// newlines. This is what step 4 shows for editing.</summary>
    public static string BuildDescriptionFullText(
        IReadOnlyList<string> prefix, IReadOnlyList<string> items, IReadOnlyList<string> suffix)
    {
        var lines = new List<string>();
        lines.AddRange(prefix);
        for (var i = 0; i < items.Count; i++)
            lines.Add($"{PersianDigits(i + 1)}- {items[i]}");
        lines.AddRange(suffix);
        return string.Join("\n", lines);
    }

    /// <summary>Replace the whole توضیحات cell with the edited full text:
    /// one paragraph per line. A line keeps the formatting of the
    /// paragraph at the same position when one exists (so editing only
    /// the words changes nothing else); extra lines clone the last
    /// numbered paragraph (numbered line) or the first paragraph.
    /// An empty text leaves a single empty paragraph so the cell stays
    /// valid. Returns false when the template has no توضیحات table.</summary>
    public static bool ApplyDescriptionFullText(WordprocessingDocument doc, string fullText)
    {
        var cell = FindDescriptionCell(doc);
        if (cell == null) return false;
        var originals = cell.Elements<Paragraph>().ToList();
        if (originals.Count == 0) return false;
        var numberedSource = originals.LastOrDefault(p =>
                DescriptionNumberRegex.IsMatch((p.InnerText ?? "").Trim()))
            ?? originals[0];
        var lines = fullText.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n')
            .Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        foreach (var para in originals) cell.RemoveChild(para);
        if (lines.Count == 0)
        {
            var empty = (Paragraph)originals[0].CloneNode(true);
            SetParagraphText(empty, "");
            cell.Append(empty);
            return true;
        }
        for (var i = 0; i < lines.Count; i++)
        {
            Paragraph source;
            if (i < originals.Count) source = originals[i];
            else if (DescriptionNumberRegex.IsMatch(lines[i])) source = numberedSource;
            else source = originals[0];
            var para = (Paragraph)source.CloneNode(true);
            SetParagraphText(para, lines[i]);
            cell.Append(para);
        }
        return true;
    }

    /// <summary>Replace the numbered توضیحات items with <paramref name="items"/>,
    /// numbered ۱- … with Persian digits. Surplus numbered paragraphs are
    /// removed; missing ones are inserted right before the closing
    /// paragraph, cloning the last numbered paragraph's run properties.
    /// The intro («توضیحات:» + the municipality paragraph) and the closing
    /// paragraph are untouched. Returns false when the template has no
    /// توضیحات table or no numbered paragraphs to anchor on.</summary>
    public static bool ApplyDescriptionItems(WordprocessingDocument doc, IReadOnlyList<string> items)
    {
        var cell = FindDescriptionCell(doc);
        if (cell == null) return false;
        List<int> NumberedIndices() => cell.Elements<Paragraph>()
            .Select((p, i) => (p, i))
            .Where(x => DescriptionNumberRegex.IsMatch((x.p.InnerText ?? "").Trim()))
            .Select(x => x.i).ToList();

        var numbered = NumberedIndices();
        if (numbered.Count == 0) return false;
        var paras = cell.Elements<Paragraph>().ToList();
        // Remove surplus numbered paragraphs from the end, so indices stay valid.
        for (var i = numbered.Count - 1; i >= items.Count; i--)
            cell.RemoveChild(paras[numbered[i]]);
        numbered = NumberedIndices();
        if (numbered.Count == 0) return false;
        paras = cell.Elements<Paragraph>().ToList();
        // Insert missing paragraphs right before the closing paragraph (the
        // paragraph after the last numbered one), cloning the last numbered
        // paragraph's runs AND its paragraph properties, so the new items
        // look identical — without the properties Word renders the inserted
        // row left-to-right (no bidi), while the rewritten rows keep theirs.
        while (numbered.Count < items.Count)
        {
            var targetIndex = numbered.Count;
            var closing = numbered[^1] + 1 < paras.Count ? paras[numbered[^1] + 1] : null;
            var source = paras[numbered[^1]];
            var newPara = new Paragraph();
            var props = source.ParagraphProperties;
            if (props != null)
                newPara.ParagraphProperties = (ParagraphProperties)props.CloneNode(true);
            foreach (var run in source.Descendants<Run>())
                newPara.Append((Run)run.CloneNode(true));
            SetParagraphText(newPara, $"{PersianDigits(targetIndex + 1)}- {items[targetIndex]}");
            if (closing != null) cell.InsertBefore(newPara, closing);
            else cell.Append(newPara);
            numbered = NumberedIndices();
            paras = cell.Elements<Paragraph>().ToList();
        }
        // Write every item (also rewrites the ones just inserted, same text).
        numbered = NumberedIndices();
        paras = cell.Elements<Paragraph>().ToList();
        for (var i = 0; i < numbered.Count && i < items.Count; i++)
            SetParagraphText(paras[numbered[i]], $"{PersianDigits(i + 1)}- {items[i]}");
        return true;
    }

    // ---------- File-level helpers ----------

    /// <summary>Read a template file into what the wizard needs (checkbox
    /// table + توضیحات items + the editable fields the template actually
    /// contains). Null when the file cannot be opened.</summary>
    public static WizardTemplateData? LoadWizardTemplateData(string templatePath)
    {
        try
        {
            using var doc = WordprocessingDocument.Open(templatePath, false);
            var checkbox = FindCheckboxTable(doc)?.Parsed;
            var split = SplitDescriptionCell(doc);
            return new WizardTemplateData(checkbox, split?.Items, PresentFieldKeys(doc),
                split?.Prefix ?? Array.Empty<string>(),
                split?.Suffix ?? Array.Empty<string>());
        }
        catch
        {
            return null;
        }
    }

    /// <summary>The editable field keys whose placeholder the template
    /// actually contains. The text is the body's joined run text, so a
    /// placeholder split across runs (« / نام ناظر / ») still counts —
    /// and only the body is scanned, because that is exactly what the
    /// generator's replacement touches (never headers or footers).
    /// «نام ناظر» counts when EITHER quote form is present («نام ناظر»
    /// or "نام ناظر").</summary>
    public static IReadOnlyList<string> PresentFieldKeys(WordprocessingDocument doc)
    {
        var body = doc.MainDocumentPart?.Document?.Body;
        var text = body == null
            ? ""
            : string.Concat(body.Descendants<Text>().Select(t => t.Text ?? ""));
        var present = new List<string>();
        foreach (var key in EditableFieldKeys)
        {
            if (key == "«نام ناظر»")
            {
                if (text.Contains("«نام ناظر»") || text.Contains("\"نام ناظر\""))
                    present.Add(key);
            }
            else if (text.Contains(key))
            {
                present.Add(key);
            }
        }
        return present;
    }

    /// <summary>Apply the wizard's edits to an already-filled report file
    /// (after the normal placeholder replacement): checkbox marks +
    /// توضیحات. When <paramref name="descriptionFullText"/> is set (step 4
    /// was edited), the whole توضیحات cell is rewritten from that text and
    /// the numbered rows are ignored; otherwise the numbered items are
    /// applied (an empty list leaves the template's توضیحات untouched).
    /// Returns (applied, marks): applied is false when the file could not
    /// be opened at all; a missing table is not an error — that edit is
    /// simply skipped (marks 0).</summary>
    public static (bool Applied, int Marks) ApplyWizardEditsToDocx(
        string docxPath,
        IReadOnlyDictionary<int, int> selections,
        IReadOnlyList<string>? descriptionItems,
        string? descriptionFullText = null)
    {
        if (selections.Count == 0 && descriptionItems == null && descriptionFullText == null) return (false, 0);
        try
        {
            using var doc = WordprocessingDocument.Open(docxPath, true);
            var marks = 0;
            if (selections.Count > 0) marks = ApplyCheckboxSelections(doc, selections);
            if (descriptionFullText != null)
            {
                var cleanLines = descriptionFullText.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n')
                    .Select(ToPersianDisplay).ToList();
                ApplyDescriptionFullText(doc, string.Join("\n", cleanLines));
            }
            else if (descriptionItems != null && descriptionItems.Count > 0)
            {
                var clean = descriptionItems.Select(ToPersianDisplay).ToList();
                ApplyDescriptionItems(doc, clean);
            }
            doc.MainDocumentPart?.Document?.Save();
            return (true, marks);
        }
        catch
        {
            return (false, 0);
        }
    }

    // ---------- Small text helpers ----------

    /// <summary>The text of one run, from its &lt;w:t&gt; elements only
    /// (same approach as StageReportGenerator.ReplaceInParagraph).</summary>
    private static string RunText(Run run) =>
        string.Concat(run.Elements<Text>().Select(t => t.Text ?? ""));

    /// <summary>Replace a run's text, keeping its properties: the first
    /// &lt;w:t&gt; gets the new text and any extra &lt;w:t&gt; elements are
    /// removed.</summary>
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

    /// <summary>Set a run's color to explicit black (000000).</summary>
    private static void SetRunColorBlack(Run run)
    {
        var rPr = run.RunProperties ?? run.PrependChild(new RunProperties());
        rPr.Color = new Color { Val = "000000" };
    }

    /// <summary>Set a paragraph's text in its first run (font/size
    /// preserved), clearing any other runs. Creates a run at 11pt when the
    /// paragraph has none.</summary>
    private static void SetParagraphText(Paragraph para, string text)
    {
        var runs = para.Descendants<Run>().ToList();
        if (runs.Count == 0)
        {
            var run = new Run(new Text(text));
            run.RunProperties = new RunProperties(new FontSize { Val = "22" });
            para.Append(run);
            return;
        }
        SetRunText(runs[0], text);
        for (var i = 1; i < runs.Count; i++) SetRunText(runs[i], "");
    }

    /// <summary>Small numbers (1..99) in Persian digits.</summary>
    private static string PersianDigits(int n) =>
        string.Concat(n.ToString().Select(c => "۰۱۲۳۴۵۶۷۸۹"[c - '0']));
}
