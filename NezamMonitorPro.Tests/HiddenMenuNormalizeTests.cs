using NezamMonitorPro.Core;
using Xunit;

namespace NezamMonitorPro.Tests;

/// <summary>
/// Pins the normalization contract of HiddenMenuService.NormalizeForCompare.
/// The test project cannot reference the WPF App assembly, so the test
/// covers the exact Core composition that method applies:
/// DigitNormalizer.ToAsciiDigits(DigitNormalizer.Normalize(input)) —
/// Persian/Arabic-Indic digits to Latin, Arabic Yeh/Kaf to Persian Yeh/Kaf,
/// trim + collapse of inner whitespace. If that composition ever changes,
/// this test fails and the hidden-menu unlock breaks with it.
/// </summary>
public class HiddenMenuNormalizeTests
{
    // Same composition as HiddenMenuService.NormalizeForCompare.
    private static string NormalizeForCompare(string? input)
        => DigitNormalizer.ToAsciiDigits(DigitNormalizer.Normalize(input));

    [Theory]
    [InlineData("۰۹۰۲۲۲۳۳۳۵۷", "09022233357")]
    [InlineData("٠٩٠٢٢٢٣٣٣٥٧", "09022233357")]
    [InlineData("09022233357", "09022233357")]
    [InlineData("  ۰۹۰۲۲۲۳۳۳۵۷  ", "09022233357")]
    [InlineData("مهديه", "مهدیه")]
    [InlineData("مكتب", "مکتب")]
    [InlineData("پوشه  من", "پوشه من")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void NormalizeForCompare_UnifiesDigitsAndLetters(string? input, string expected)
        => Assert.Equal(expected, NormalizeForCompare(input));

    [Fact]
    public void PersianKeyboardDigits_MatchLatinFolderName()
    {
        // The exact reported case: the folder name typed with a Persian
        // keyboard (Persian digits) must unlock the same as the Latin-digit
        // name stored on Drive.
        Assert.Equal(NormalizeForCompare("09022233357"), NormalizeForCompare("۰۹۰۲۲۲۳۳۳۵۷"));
    }

    [Fact]
    public void ArabicIndicDigits_MatchLatinFolderName()
    {
        Assert.Equal(NormalizeForCompare("09022233357"), NormalizeForCompare("٠٩٠٢٢٢٣٣٣٥٧"));
    }
}
