using Budgie.Modules.EmailImport.Application.Parsing.Normalization;
using FluentAssertions;
using Xunit;

namespace Budgie.Modules.EmailImport.UnitTests.Parsing;

/// <summary>
/// <see cref="AmountNormalizer"/> is the single place where a bank email's number becomes a
/// decimal. The case that matters most is <c>1.234</c>: three digits behind the only separator
/// are ambiguous by shape alone, and only the user's language can resolve it. A naive
/// implementation breaks exactly there — in BOTH directions.
/// </summary>
public class AmountNormalizerTests
{
    // ── The ambiguous case that decides everything else ────────────────────────────────
    [Theory]
    [InlineData("1.234", "es", 1234)]      // '.' is the Spanish thousands separator
    [InlineData("1.234", "en", 1.234)]      // '.' is the English decimal point
    [InlineData("1,234", "es", 1.234)]      // symmetric: ',' is NOT thousands in Spanish
    [InlineData("1,234", "en", 1234)]
    public void TryParse_ThreeDigitsBehindOnlySeparator_UsesLanguage(
        string raw, string language, decimal expected)
    {
        AmountNormalizer.TryParse(raw, language, out var value).Should().BeTrue();
        value.Should().Be(expected);
    }

    [Fact]
    public void TryParse_AmbiguousCase_ProducesDifferentValuesPerLanguage()
    {
        AmountNormalizer.TryParse("1.234", "es", out var spanish).Should().BeTrue();
        AmountNormalizer.TryParse("1.234", "en", out var english).Should().BeTrue();

        spanish.Should().NotBe(english);
        spanish.Should().Be(1234m);
        english.Should().Be(1.234m);
    }

    [Fact]
    public void TryParse_UnknownLanguage_TreatsAsEnglish()
    {
        // Anything that is not "es" resolves to the English convention (AmountNormalizer.cs:123).
        AmountNormalizer.TryParse("1.234", "pt", out var value).Should().BeTrue();
        value.Should().Be(1.234m);
    }

    [Fact]
    public void TryParse_NullLanguage_TreatsAsEnglish()
    {
        AmountNormalizer.TryParse("1.234", null!, out var value).Should().BeTrue();
        value.Should().Be(1.234m);
    }

    [Fact]
    public void TryParse_LanguagePrefixMatch_TreatsEsVariantsAsSpanish()
    {
        // IsSpanish uses StartsWith("es"), so "es-ES" is Spanish.
        AmountNormalizer.TryParse("1.234", "es-ES", out var value).Should().BeTrue();
        value.Should().Be(1234m);
    }

    // ── Both separators present: the LAST one is the decimal, the language is irrelevant ──
    [Theory]
    [InlineData("1.234,56", "es", 1234.56)]
    [InlineData("1.234,56", "en", 1234.56)]
    [InlineData("1,234.56", "es", 1234.56)]
    [InlineData("1,234.56", "en", 1234.56)]
    [InlineData("1.234.567,89", "es", 1234567.89)]
    [InlineData("1,234,567.89", "en", 1234567.89)]
    public void TryParse_BothSeparatorsPresent_LastSeparatorIsTheDecimalPoint(
        string raw, string language, decimal expected)
    {
        // With both separators on the table nothing is ambiguous, so the language plays no
        // part at all (AmountNormalizer.cs:80). This is what stops "1.234,56" from being
        // mangled for an English-locale user reading a Spanish bank email.
        AmountNormalizer.TryParse(raw, language, out var value).Should().BeTrue();
        value.Should().Be(expected);
    }

    [Fact]
    public void TryParse_BothSeparatorsWithThreeDecimals_IsRejected()
    {
        // More than two decimals behind the last separator is never a valid amount.
        AmountNormalizer.TryParse("1.2345,678", "es", out _).Should().BeFalse();
    }

    // ── Single separator, one or two decimals behind it: never ambiguous ───────────────
    [Theory]
    [InlineData("45,20", "es", 45.20)]
    [InlineData("45,20", "en", 45.20)]
    [InlineData("45.20", "es", 45.20)]
    [InlineData("45.20", "en", 45.20)]
    public void TryParse_AtMostTwoDecimals_IsLanguageIndependent(string raw, string language, decimal expected)
    {
        AmountNormalizer.TryParse(raw, language, out var value).Should().BeTrue();
        value.Should().Be(expected);
    }

    // ── Currency symbols and stray letters are stripped before parsing ──────────────────
    [Theory]
    [InlineData("45,20 €", "es", 45.20)]
    [InlineData("$1,234.56", "en", 1234.56)]
    [InlineData("45,20&euro;", "es", 45.20)]  // HTML entity left in the text
    [InlineData("EUR 45,20", "es", 45.20)]
    [InlineData("45,20 EUR", "es", 45.20)]
    public void TryParse_CurrencySymbolsAndLetters_StripsThem(string raw, string language, decimal expected)
    {
        AmountNormalizer.TryParse(raw, language, out var value).Should().BeTrue();
        value.Should().Be(expected);
    }

    // ── Signs: only at the front, and the sign is preserved ────────────────────────────
    [Fact]
    public void TryParse_LeadingMinus_KeepsTheSign()
    {
        AmountNormalizer.TryParse("-12.00", "en", out var value).Should().BeTrue();
        value.Should().Be(-12m);
    }

    [Fact]
    public void TryParse_LeadingPlus_KeepsThePositiveValue()
    {
        AmountNormalizer.TryParse("+12.00", "en", out var value).Should().BeTrue();
        value.Should().Be(12m);
    }

    [Theory]
    [InlineData("12-00")]   // a sign anywhere but the front means "not a number"
    [InlineData("12+00")]
    [InlineData("1-2.50")]
    public void TryParse_SignNotAtTheFront_IsRejected(string raw)
    {
        AmountNormalizer.TryParse(raw, "en", out _).Should().BeFalse();
    }

    // ── Repeated separators of one type ────────────────────────────────────────────────
    [Fact]
    public void TryParse_GroupedThousandsWithSingleSeparatorType_DropsEverySeparator()
    {
        // 3 digits behind the last '.' → grouping, in ANY language (AmountNormalizer.cs:98).
        AmountNormalizer.TryParse("1.234.567", "es", out var spanish).Should().BeTrue();
        spanish.Should().Be(1234567m);

        AmountNormalizer.TryParse("1,234,567", "en", out var english).Should().BeTrue();
        english.Should().Be(1234567m);
    }

    [Fact]
    public void TryParse_RepeatedSeparatorWithTwoTrailingDigits_MakesTheLastOneTheDecimalPoint()
    {
        // "1,234,56" → the last comma is the decimal point, the earlier ones group.
        AmountNormalizer.TryParse("1,234,56", "en", out var value).Should().BeTrue();
        value.Should().Be(1234.56m);
    }

    [Fact]
    public void TryParse_RepeatedSeparatorWithOneTrailingDigit_MakesTheLastOneTheDecimalPoint()
    {
        // 1-2 digits behind the last separator can never be a thousands group.
        AmountNormalizer.TryParse("12,345,678,9", "en", out var value).Should().BeTrue();
        value.Should().Be(12345678.9m);
    }

    [Fact]
    public void TryParse_RepeatedSeparatorWithFourTrailingDigits_IsRejected()
    {
        // Forcing a decimal reading with more than two decimals is refused.
        AmountNormalizer.TryParse("1.234.5678", "es", out _).Should().BeFalse();
    }

    // ── NBSP and narrow NBSP: some banks use them as thousands separators ──────────────
    [Theory]
    [InlineData("1\u00A0234,56", "es", 1234.56)]
    [InlineData("1\u202F234,56", "es", 1234.56)]
    [InlineData("1\u00A0234,56", "en", 1234.56)]
    [InlineData("1\u202F234,56", "en", 1234.56)]
    public void TryParse_NoBreakSpaces_TreatsThemAsNoise(string raw, string language, decimal expected)
    {
        AmountNormalizer.TryParse(raw, language, out var value).Should().BeTrue();
        value.Should().Be(expected);
    }

    // ── Plain integers ─────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData("1234", 1234)]
    [InlineData("5", 5)]
    [InlineData("0", 0)]
    public void TryParse_PlainInteger_IsAccepted(string raw, decimal expected)
    {
        AmountNormalizer.TryParse(raw, "es", out var value).Should().BeTrue();
        value.Should().Be(expected);
    }

    // ── Rejections: never throw, always return false ───────────────────────────────────
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("€")]
    [InlineData("1.000,")]        // trailing separator
    [InlineData("1,000.")]
    public void TryParse_InvalidInput_ReturnsFalseWithoutThrowing(string raw)
    {
        var act = () => AmountNormalizer.TryParse(raw, "es", out _);
        act.Should().NotThrow();
        act().Should().BeFalse();
    }

    [Fact]
    public void TryParse_Rejected_LeavesTheOutParameterAtZero()
    {
        AmountNormalizer.TryParse("abc", "es", out var value).Should().BeFalse();
        value.Should().Be(0m);
    }

    [Fact]
    public void TryParse_OutParameter_OnFailureDoesNotLeakThePreviousValue()
    {
        AmountNormalizer.TryParse("1.234", "es", out var first).Should().BeTrue();
        first.Should().Be(1234m);

        AmountNormalizer.TryParse("nonsense", "es", out var second).Should().BeFalse();
        second.Should().Be(0m);
    }

    [Fact]
    public void TryParse_DecimalArithmetic_IsExact()
    {
        // Exact decimal arithmetic, no binary float drift: 0.1 + 0.2 must be exactly 0.3.
        AmountNormalizer.TryParse("0,10", "es", out var a).Should().BeTrue();
        AmountNormalizer.TryParse("0,20", "es", out var b).Should().BeTrue();
        (a + b).Should().Be(0.30m);
    }

    [Theory]
    [InlineData("0,07", "es", 0.07)]
    [InlineData("0.07", "en", 0.07)]
    [InlineData("0,755", "es", 0.755)]
    public void TryParse_ThreeSignificantDecimals_AreExact(string raw, string language, decimal expected)
    {
        AmountNormalizer.TryParse(raw, language, out var value).Should().BeTrue();
        value.Should().Be(expected);
    }
}
