using Budgie.Shared.Domain.ValueObjects;
using FluentAssertions;

namespace Budgie.Shared.UnitTests.Domain.ValueObjects;

public sealed class CurrencyCodeTests
{
    [Fact]
    public void From_WithValid3LetterCode_ReturnsCurrencyCode()
    {
        var code = CurrencyCode.From("USD");
        code.Code.Should().Be("USD");
    }

    [Fact]
    public void From_WithLowercase_AutoUppercases()
    {
        var code = CurrencyCode.From("eur");
        code.Code.Should().Be("EUR");
    }

    [Fact]
    public void From_WithWhitespace_Trims()
    {
        var code = CurrencyCode.From("  gbp ");
        code.Code.Should().Be("GBP");
    }

    [Fact]
    public void From_WithNull_ThrowsArgumentException()
    {
        var act = () => CurrencyCode.From(null!);
        act.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Fact]
    public void From_WithEmpty_ThrowsArgumentException()
    {
        var act = () => CurrencyCode.From("");
        act.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Fact]
    public void From_With2Chars_ThrowsArgumentException()
    {
        var act = () => CurrencyCode.From("US");
        act.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Fact]
    public void From_With4Chars_ThrowsArgumentException()
    {
        var act = () => CurrencyCode.From("USDD");
        act.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Fact]
    public void Equality_SameCode_AreEqual()
    {
        var a = CurrencyCode.From("USD");
        var b = CurrencyCode.From("USD");
        a.Should().Be(b);
    }

    [Fact]
    public void ImplicitConversionToString_ReturnsCode()
    {
        var code = CurrencyCode.From("USD");
        string result = code;
        result.Should().Be("USD");
    }

    [Fact]
    public void ToString_ReturnsCode()
    {
        var code = CurrencyCode.From("EUR");
        code.ToString().Should().Be("EUR");
    }
}
