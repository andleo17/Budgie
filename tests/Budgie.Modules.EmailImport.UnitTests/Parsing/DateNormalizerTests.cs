using Budgie.Modules.EmailImport.Application.Parsing.Normalization;
using FluentAssertions;
using Xunit;

namespace Budgie.Modules.EmailImport.UnitTests.Parsing;

/// <summary>
/// <see cref="DateNormalizer"/> resolves the ambiguous numeric formats with the year last
/// (<c>01/02/2026</c> is 1 February in Spanish and 2 January in English) and accepts textual
/// month names in both orders. Every successful parse is
/// <see cref="DateTimeKind.Unspecified"/> — the staging column is TIMESTAMP WITHOUT TIME ZONE
/// and a value carrying an implicit zone would silently shift.
/// </summary>
public class DateNormalizerTests
{
    // ── Numeric with the year last: the language decides the order ──────────────────────
    [Theory]
    [InlineData("01/02/2026", "es", 2026, 2, 1)]
    [InlineData("01/02/2026", "en", 2026, 1, 2)]
    public void TryParse_NumericWithYearLast_UsesLanguageOrder(
        string raw, string language, int year, int month, int day)
    {
        DateNormalizer.TryParse(raw, language, out var value).Should().BeTrue();
        value.Should().Be(new DateTime(year, month, day));
    }

    [Fact]
    public void TryParse_AmbiguousNumericDate_ProducesDifferentDatesPerLanguage()
    {
        DateNormalizer.TryParse("01/02/2026", "es", out var spanish).Should().BeTrue();
        DateNormalizer.TryParse("01/02/2026", "en", out var english).Should().BeTrue();

        spanish.Should().NotBe(english);
        spanish.Should().Be(new DateTime(2026, 2, 1));
        english.Should().Be(new DateTime(2026, 1, 2));
    }

    [Fact]
    public void TryParse_UnknownLanguage_TreatsAsEnglish()
    {
        DateNormalizer.TryParse("01/02/2026", "de", out var value).Should().BeTrue();
        value.Should().Be(new DateTime(2026, 1, 2));
    }

    // ── The deliberate numeric fallback (DateNormalizer.cs:74-79) ──────────────────────
    [Fact]
    public void TryParse_EnglishUserReadingSpanishBankDate_FallsBackToTheOtherOrder()
    {
        // 25/12 is impossible month-first, so it is re-read as 25 December. This is the
        // conscious trade-off documented in the XML doc: a Spanish bank email read by an
        // English-locale user is far more common than a real "month 25".
        DateNormalizer.TryParse("25/12/2026", "en", out var value).Should().BeTrue();
        value.Should().Be(new DateTime(2026, 12, 25));
    }

    [Fact]
    public void TryParse_ImpossibleUnderThePreferredOrder_ReinterpretsInsteadOfFailing()
    {
        // "month 13" only parses because the swapped order makes it 13 January.
        DateNormalizer.TryParse("01/13/2026", "es", out var value).Should().BeTrue();
        value.Should().Be(new DateTime(2026, 1, 13));
    }

    [Theory]
    [InlineData("13/13/2026")]   // impossible under BOTH orders
    [InlineData("32/01/2026")]   // day 32 in either position
    [InlineData("00/01/2026")]   // zero in either position
    [InlineData("01/00/2026")]
    public void TryParse_ImpossibleUnderBothOrders_IsRejectedWithoutThrowing(string raw)
    {
        var act = () => DateNormalizer.TryParse(raw, "es", out _);
        act.Should().NotThrow();
        act().Should().BeFalse();
    }

    [Theory]
    [InlineData("32/01/2026")]
    [InlineData("01/32/2026")]
    public void TryParse_ImpossibleNumericDate_IsRejectedUnderEitherLanguage(string raw)
    {
        DateNormalizer.TryParse(raw, "es", out _).Should().BeFalse();
        DateNormalizer.TryParse(raw, "en", out _).Should().BeFalse();
    }

    [Fact]
    public void TryParse_February30th_IsRejected()
    {
        // Day > DaysInMonth is the explicit calendar check (DateNormalizer.cs:123).
        DateNormalizer.TryParse("30/02/2026", "es", out _).Should().BeFalse();
    }

    [Fact]
    public void TryParse_January32nd_IsRejected()
    {
        DateNormalizer.TryParse("32/01/2026", "es", out _).Should().BeFalse();
    }

    [Fact]
    public void TryParse_February29thOfLeapYear_IsAccepted()
    {
        DateNormalizer.TryParse("29/02/2028", "es", out var value).Should().BeTrue();
        value.Should().Be(new DateTime(2028, 2, 29));
    }

    [Fact]
    public void TryParse_February29thOfNonLeapYear_IsRejected()
    {
        DateNormalizer.TryParse("29/02/2026", "es", out _).Should().BeFalse();
    }

    // ── ISO: the year comes first, so the reading is language-independent ──────────────
    [Theory]
    [InlineData("2026-10-02", "es")]
    [InlineData("2026-10-02", "en")]
    [InlineData("2026/10/02", "es")]
    [InlineData("2026.10.02", "en")]
    public void TryParse_IsoLike_IsLanguageIndependent(string raw, string language)
    {
        DateNormalizer.TryParse(raw, language, out var value).Should().BeTrue();
        value.Should().Be(new DateTime(2026, 10, 2));
    }

    // ── Month names, day-first (the Spanish default) ───────────────────────────────────
    [Theory]
    [InlineData("2 oct 2026")]
    [InlineData("2 de octubre de 2026")]
    [InlineData("2 oct. 2026")]
    [InlineData("02 octubre 2026")]
    public void TryParse_DayFirstSpanishMonthName_IsAccepted(string raw)
    {
        DateNormalizer.TryParse(raw, "es", out var value).Should().BeTrue();
        value.Should().Be(new DateTime(2026, 10, 2));
    }

    [Fact]
    public void TryParse_DayFirstAbbreviatedEnglishMonthName_IsAccepted()
    {
        DateNormalizer.TryParse("2 Oct 2026", "en", out var value).Should().BeTrue();
        value.Should().Be(new DateTime(2026, 10, 2));
    }

    // ── Month names, month-first (the English default) ─────────────────────────────────
    [Theory]
    [InlineData("October 2, 2026", "en")]
    [InlineData("Oct 2, 2026", "en")]
    [InlineData("Oct. 2, 2026", "en")]
    [InlineData("October 2 2026", "en")]
    public void TryParse_MonthFirstEnglishMonthName_IsAccepted(string raw, string language)
    {
        DateNormalizer.TryParse(raw, language, out var value).Should().BeTrue();
        value.Should().Be(new DateTime(2026, 10, 2));
    }

    [Theory]
    [InlineData("octubre 2, 2026", "es")]
    [InlineData("octubre 2 de 2026", "es")]
    [InlineData("Oct. 2, 2026", "es")]
    public void TryParse_MonthFirstSpanishMonthName_IsAccepted(string raw, string language)
    {
        DateNormalizer.TryParse(raw, language, out var value).Should().BeTrue();
        value.Should().Be(new DateTime(2026, 10, 2));
    }

    [Theory]
    [InlineData("septiembre", "2026-09-01")]
    [InlineData("setiembre", "2026-09-01")]
    [InlineData("sept.", "2026-09-01")]
    [InlineData("ene.", "2026-01-01")]
    [InlineData("mayo", "2026-05-01")]
    [InlineData("may", "2026-05-01")]
    public void TryParse_MonthNameDictionary_CoversEveryMonthAndAbbreviation(
        string monthName, string expectedIso)
    {
        var raw = $"1 {monthName} 2026";
        DateNormalizer.TryParse(raw, "es", out var value).Should().BeTrue();
        value.Should().Be(DateTime.Parse(expectedIso, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void TryParse_MonthName_IsCaseInsensitive()
    {
        DateNormalizer.TryParse("1 OCTUBRE 2026", "es", out var value).Should().BeTrue();
        value.Should().Be(new DateTime(2026, 10, 1));
    }

    [Fact]
    public void TryParse_MonthFirstWithImpossibleDay_IsRejectedAndNotReinterpreted()
    {
        // Unlike the numeric branch there is NO fallback here: a month name can never be
        // reinterpreted as a day, so October 32nd is simply wrong.
        DateNormalizer.TryParse("October 32, 2026", "en", out _).Should().BeFalse();
    }

    // ── Kind is always Unspecified ─────────────────────────────────────────────────────
    [Theory]
    [InlineData("01/02/2026", "es")]
    [InlineData("2026-10-02", "en")]
    [InlineData("October 2, 2026", "en")]
    [InlineData("2 de octubre de 2026", "es")]
    public void TryParse_AnyAcceptedFormat_AlwaysReturnsUnspecified(string raw, string language)
    {
        DateNormalizer.TryParse(raw, language, out var value).Should().BeTrue();
        value.Kind.Should().Be(DateTimeKind.Unspecified);
    }

    [Fact]
    public void TryParse_ParsedDate_HasNoTimeComponent()
    {
        DateNormalizer.TryParse("01/02/2026", "es", out var value).Should().BeTrue();
        value.TimeOfDay.Should().Be(TimeSpan.Zero);
    }

    // ── Rejections: never throw ────────────────────────────────────────────────────────
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no date here")]
    [InlineData("99/99/9999")]
    public void TryParse_UnparseableInput_ReturnsFalseWithoutThrowing(string raw)
    {
        var act = () => DateNormalizer.TryParse(raw, "es", out _);
        act.Should().NotThrow();
        act().Should().BeFalse();
    }

    [Fact]
    public void TryParse_Rejected_LeavesTheOutParameterAtDefault()
    {
        DateNormalizer.TryParse("nonsense", "es", out var value).Should().BeFalse();
        value.Should().Be(default);
    }

    [Fact]
    public void TryParse_SurroundingWhitespace_IsTrimmed()
    {
        DateNormalizer.TryParse("  01/02/2026  ", "es", out var value).Should().BeTrue();
        value.Should().Be(new DateTime(2026, 2, 1));
    }
}
