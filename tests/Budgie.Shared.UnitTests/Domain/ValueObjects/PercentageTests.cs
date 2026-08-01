using Budgie.Shared.Domain.ValueObjects;
using FluentAssertions;

namespace Budgie.Shared.UnitTests.Domain.ValueObjects;

public sealed class PercentageTests
{
    [Fact]
    public void From_With0_ReturnsPercentage()
    {
        var pct = Percentage.From(0);
        pct.Value.Should().Be(0);
    }

    [Fact]
    public void From_With100_ReturnsPercentage()
    {
        var pct = Percentage.From(100);
        pct.Value.Should().Be(100);
    }

    [Fact]
    public void From_With50Point5_ReturnsPercentage()
    {
        var pct = Percentage.From(50.5m);
        pct.Value.Should().Be(50.5m);
    }

    [Fact]
    public void From_WithNegative0Point01_ThrowsArgumentException()
    {
        var act = () => Percentage.From(-0.01m);
        act.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Fact]
    public void From_With100Point01_ThrowsArgumentException()
    {
        var act = () => Percentage.From(100.01m);
        act.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Fact]
    public void From_WithNegative1_ThrowsArgumentException()
    {
        var act = () => Percentage.From(-1m);
        act.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Fact]
    public void From_With101_ThrowsArgumentException()
    {
        var act = () => Percentage.From(101m);
        act.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var a = Percentage.From(75m);
        var b = Percentage.From(75m);
        a.Should().Be(b);
    }

    [Fact]
    public void ImplicitConversionToDecimal_ReturnsValue()
    {
        var pct = Percentage.From(33.3m);
        decimal result = pct;
        result.Should().Be(33.3m);
    }

    [Fact]
    public void ToString_ReturnsFormattedWithPercent()
    {
        var pct = Percentage.From(42.5m);
        pct.ToString().Should().Be("42.5%");
    }
}
