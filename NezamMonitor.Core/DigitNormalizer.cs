using System.Text;

namespace NezamMonitor.Core;

/// <summary>
/// Centralized digit normalization service.
/// Converts Persian/Arabic-Indic digits to ASCII canonical form,
/// and ASCII digits to Persian for presentation.
/// Only modifies digit characters — never touches separators or text.
/// </summary>
public static class DigitNormalizer
{
    /// <summary>
    /// Normalize any digit (Persian, Arabic-Indic, or ASCII) to ASCII digit.
    /// Only modifies digit characters. / and . and - remain untouched.
    /// </summary>
    public static string ToAsciiDigits(string? input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        var sb = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            // Persian digits ۰-۹
            if (ch >= '\u06F0' && ch <= '\u06F9')
                sb.Append((char)('0' + (ch - '\u06F0')));
            // Arabic-Indic digits ٠-٩
            else if (ch >= '\u0660' && ch <= '\u0669')
                sb.Append((char)('0' + (ch - '\u0660')));
            // ASCII digits remain unchanged
            else
                sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Convert ASCII digits to Persian digits for Word/UI presentation.
    /// Only modifies digit characters 0-9. All other characters pass through.
    /// </summary>
    public static string ToPersianDigits(string? input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        var sb = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            if (ch >= '0' && ch <= '9')
                sb.Append((char)('\u06F0' + (ch - '0')));
            else
                sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Convert ASCII digits to Arabic-Indic digits.
    /// Only modifies digit characters. All other characters pass through.
    /// </summary>
    public static string ToArabicDigits(string? input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        var sb = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            if (ch >= '0' && ch <= '9')
                sb.Append((char)('\u0660' + (ch - '0')));
            else
                sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Normalize source text: unify Persian/Arabic chars, normalize digits,
    /// trim, collapse whitespace. For comparison keys.
    /// </summary>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        var s = UnifyPersianChars(input).Trim();
        s = string.Join(' ', s.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries));
        return s;
    }

    /// <summary>
    /// Unify Arabic Yeh/Kaf to Persian equivalents. Preserves line breaks.
    /// </summary>
    public static string UnifyPersianChars(string? input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return input.Replace('\u064A', '\u06CC').Replace('\u0643', '\u06A9');
    }

    /// <summary>
    /// Check if value is empty or the site's empty marker '_'.
    /// </summary>
    public static bool IsEmptyMarker(string? input)
        => string.IsNullOrWhiteSpace(input) || Normalize(input) == "_";

    /// <summary>
    /// Sanitize illegal Windows filename characters.
    /// </summary>
    public static string SanitizeFileName(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(input.Length);
        foreach (var ch in input)
            sb.Append(Array.IndexOf(invalid, ch) >= 0 ? '-' : ch);
        var result = sb.ToString().Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(result) ? "_" : result;
    }

    /// <summary>
    /// For filename only: 1404/568 → 1404-568
    /// Uses ASCII canonical form.
    /// </summary>
    public static string ForFilename(string caseNumber)
    {
        var ascii = ToAsciiDigits(caseNumber ?? "");
        return ascii.Replace('/', '-');
    }

    /// <summary>
    /// For filename only: reverse case number for folder naming.
    /// 1404/568 → 568-1404
    /// Uses ASCII canonical form.
    /// </summary>
    public static string ReverseForFilename(string caseNumber)
    {
        var ascii = ToAsciiDigits(caseNumber ?? "");
        var parts = ascii.Split('/');
        return parts.Length == 2 ? $"{parts[1].Trim()}-{parts[0].Trim()}" : Normalize(caseNumber);
    }
}
