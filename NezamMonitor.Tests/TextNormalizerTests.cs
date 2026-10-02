using NezamMonitor.Core;
using Xunit;

namespace NezamMonitor.Tests;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("۱۴۰۴/۵۱۴", "1404/514")]
    [InlineData("۴۵۶۵", "4565")]
    [InlineData("٠١٢", "012")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void ToLatinDigits_ConvertsCorrectly(string? input, string expected)
        => Assert.Equal(expected, TextNormalizer.ToLatinDigits(input));

    [Theory]
    [InlineData("1404/514", "۱۴۰۴/۵۱۴")]
    [InlineData("4565", "۴۵۶۵")]
    public void ToPersianDigits_ConvertsCorrectly(string input, string expected)
        => Assert.Equal(expected, TextNormalizer.ToPersianDigits(input));

    [Fact]
    public void Normalize_UnifiesArabicAndPersianYeh()
    {
        // Arabic Yeh U+064A vs Persian Yeh U+06CC must normalize identically
        var arabic = "مهديه";  // U+064A Arabic Yeh
        var persian = "مهدیه"; // U+06CC Persian Yeh
        Assert.Equal(TextNormalizer.Normalize(persian), TextNormalizer.Normalize(arabic));
    }

    [Theory]
    [InlineData("_", true)]
    [InlineData(" _ ", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    [InlineData("1404", false)]
    public void IsEmptyMarker_DetectsPlaceholder(string? input, bool expected)
        => Assert.Equal(expected, TextNormalizer.IsEmptyMarker(input));

    [Theory]
    [InlineData("سمیرا<رجبی>", "سمیرا-رجبی-")]
    [InlineData("file:name?.doc", "file-name-.doc")]
    [InlineData("1404/514", "1404-514")]
    public void SanitizeFileName_ReplacesIllegalChars(string input, string expected)
        => Assert.Equal(expected, TextNormalizer.SanitizeFileName(input));

    [Theory]
    [InlineData("1404/631", "631-1404")]
    [InlineData("۱۴۰۴/۶۳۰", "630-1404")]
    [InlineData("no-slash", "no-slash")]
    public void ReverseCaseNumber_ReversesCorrectly(string input, string expected)
        => Assert.Equal(expected, TextNormalizer.ReverseCaseNumber(input));
}
