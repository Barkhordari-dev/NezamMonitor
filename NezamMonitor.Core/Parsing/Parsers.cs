using System.Text.RegularExpressions;
using NezamMonitor.Core.Models;

namespace NezamMonitor.Core.Parsing;

/// <summary>Parses engineer/supervisor dialog text (ناظرین پرونده).</summary>
public static partial class EngineerParser
{
    [GeneratedRegex(@"(معماری|معماري)\s*:\s*\r?\n?\s*(.+)")]
    private static partial Regex ArchRx();

    [GeneratedRegex(@"(عمران)\s*:\s*\r?\n?\s*(.+)")]
    private static partial Regex CivilRx();

    [GeneratedRegex(@"(مکانیک|مكانيك)\s*:\s*\r?\n?\s*(.+)")]
    private static partial Regex MechRx();

    [GeneratedRegex(@"(برق)\s*:\s*\r?\n?\s*(.+)")]
    private static partial Regex ElecRx();

    [GeneratedRegex(@"(هماهنگ\s*(?:کننده|كننده))\s*:\s*\r?\n?\s*(.+)")]
    private static partial Regex CoordRx();

    public static List<Engineer> Parse(string text)
    {
        var result = new List<Engineer>();
        void Add(string discipline, Match m)
        {
            if (m is null || !m.Success) return;
            var name = m.Groups[2].Value.Trim().Split('\n')[0].Trim();
            if (string.IsNullOrEmpty(name) || name.Contains(':')) return;
            name = TextNormalizer.Normalize(name);
            result.Add(new Engineer(TextNormalizer.Normalize(discipline), name));
        }

        Add("معماری", ArchRx().Match(text));
        Add("عمران", CivilRx().Match(text));
        Add("مکانیک", MechRx().Match(text));
        Add("برق", ElecRx().Match(text));
        Add("هماهنگ‌کننده", CoordRx().Match(text));
        return result;
    }
}

/// <summary>Parses the specifications dialog (مشخصات بلوک).</summary>
public static partial class SpecificationParser
{
    private static (string Key, Regex Rx)[] Patterns()
    {
        Regex Rx(string label) =>
            new Regex(Regex.Escape(label) + @"\s*:\s*\r?\n\s*(.+)", RegexOptions.Multiline);
        return new[]
        {
            ("BuildingGroup",        Rx("گروه ساختمانی")),
            ("RenovationCode",       Rx("کد نوسازی")),
            ("PlanInstructionNo",    Rx("شماره دستور تهیه نقشه")),
            ("PlanInstructionType",  Rx("نوع دستور تهیه نقشه")),
            ("LandArea",             Rx("مساحت زمین")),
            ("ParafArea",            Rx("متراژ پاراف")),
            ("CapacityArea",         Rx("متراژ کسر ظرفیت نظارت")),
            ("PlanInstructionDate",  Rx("تاریخ دستور تهیه نقشه")),
            ("StructureType",        Rx("نوع سازه")),
            ("BlockTitle",           Rx("عنوان بلوک")),
            ("BlockCount",           Rx("تعداد بلوک")),
            ("Floors",               Rx("تعداد طبقات")),
            ("Units",                Rx("تعداد واحد")),
            ("Issuer",               Rx("صادر کننده")),
            ("PermitNumber",         Rx("شماره پروانه")),
            ("PermitDate",           Rx("تاریخ صدور پروانه")),
            ("ReleaseDate",          Rx("تاریخ ترخیص")),
            ("PlanZone",             Rx("محدوده طرح")),
            ("Address",              Rx("آدرس")),
        };
    }

    public static CaseSpecification Parse(string text)
    {
        var spec = new CaseSpecification();
        // Unify Arabic Yeh/Kaf but PRESERVE line breaks for line-based regex
        text = TextNormalizer.UnifyPersianChars(text);
        foreach (var (key, rx) in Patterns())
        {
            var m = rx.Match(text);
            if (!m.Success) continue;
            var value = m.Groups[1].Value.Trim().Split('\n')[0].Trim();
            if (TextNormalizer.IsEmptyMarker(value)) continue;
            typeof(CaseSpecification).GetProperty(key)?.SetValue(spec, value);
        }
        return spec;
    }
}

/// <summary>Parses the fee dialog tab-separated table rows (حق الزحمه).</summary>
public static class FeeParser
{
    public static List<Fee> Parse(string text)
    {
        var fees = new List<Fee>();
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (!line.Contains("ریال")) continue;
            var parts = line.Split('\t');
            if (parts.Length < 7) continue;

            string G(int i) => i < parts.Length ? parts[i].Trim() : "";

            fees.Add(new Fee(
                Discipline:     G(0),
                ServiceType:    G(1),
                Stage:          G(6),
                StartDate:      G(3),
                EndDate:        G(4),
                Amount:         G(5),
                PayStatus:      G(7),
                ConfirmStatus:  G(8),
                AmountType:     G(9),
                Description:    G(10)));
        }
        return fees;
    }
}

/// <summary>Parses the reports table page (گزارش ها).</summary>
public static class ReportParser
{
    /// <summary>
    /// Parse rows from a pre-extracted table representation.
    /// Each row is a list of cell texts; the first row may be the header.
    /// </summary>
    public static List<ReportRecord> ParseRows(IEnumerable<IReadOnlyList<string>> rows)
    {
        var records = new List<ReportRecord>();
        foreach (var cells in rows)
        {
            if (cells.Count < 8) continue;
            var first = TextNormalizer.ToLatinDigits(cells[0].Trim());
            if (first.Length == 0 || !first.All(char.IsDigit)) continue;

            string G(int i) => i < cells.Count ? cells[i].Trim() : "";

            records.Add(new ReportRecord(
                RowNo:        G(0),
                ReportType:   G(1),
                Stage:        G(2),
                Engineer:     G(3),
                Discipline:   G(4),
                VisitDate:    G(5),
                CeilingCount: G(6),
                Indicator:    G(7),
                HasFile:      cells.Count > 8 && G(8).Length >= 0 && cells.Count > 8));
        }
        return records;
    }
}
