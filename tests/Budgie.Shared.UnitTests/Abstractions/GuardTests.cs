using Budgie.Shared.Abstractions;
using FluentAssertions;

namespace Budgie.Shared.UnitTests.Abstractions;

public sealed class GuardTests
{
    [Fact]
    public void NotNullOrEmpty_WithValidString_ReturnsIt()
    {
        var result = Guard.NotNullOrEmpty("hello", "test");
        result.Should().Be("hello");
    }

    [Fact]
    public void NotNullOrEmpty_WithNull_ThrowsArgumentException()
    {
        var act = () => Guard.NotNullOrEmpty(null, "test");
        act.Should().Throw<ArgumentException>().WithParameterName("test");
    }

    [Fact]
    public void NotNullOrEmpty_WithEmpty_ThrowsArgumentException()
    {
        var act = () => Guard.NotNullOrEmpty("", "test");
        act.Should().Throw<ArgumentException>().WithParameterName("test");
    }

    [Fact]
    public void NotNullOrEmpty_WithWhitespace_ThrowsArgumentException()
    {
        var act = () => Guard.NotNullOrEmpty("   ", "test");
        act.Should().Throw<ArgumentException>().WithParameterName("test");
    }

    [Fact]
    public void NotNull_WithObject_ReturnsIt()
    {
        var obj = new object();
        var result = Guard.NotNull(obj, "test");
        result.Should().BeSameAs(obj);
    }

    [Fact]
    public void NotNull_WithNull_ThrowsArgumentException()
    {
        object? obj = null;
        var act = () => Guard.NotNull(obj, "test");
        act.Should().Throw<ArgumentException>().WithParameterName("test");
    }

    [Fact]
    public void Positive_Decimal_WithPositive_ReturnsIt()
    {
        var result = Guard.Positive(5.5m, "test");
        result.Should().Be(5.5m);
    }

    [Fact]
    public void Positive_Decimal_With0_ThrowsArgumentException()
    {
        var act = () => Guard.Positive(0m, "test");
        act.Should().Throw<ArgumentException>().WithParameterName("test");
    }

    [Fact]
    public void Positive_Decimal_WithNegative_ThrowsArgumentException()
    {
        var act = () => Guard.Positive(-1m, "test");
        act.Should().Throw<ArgumentException>().WithParameterName("test");
    }

    [Fact]
    public void NonNegative_Decimal_With0_ReturnsIt()
    {
        var result = Guard.NonNegative(0m, "test");
        result.Should().Be(0m);
    }

    [Fact]
    public void NonNegative_Decimal_WithNegative_ThrowsArgumentException()
    {
        var act = () => Guard.NonNegative(-0.01m, "test");
        act.Should().Throw<ArgumentException>().WithParameterName("test");
    }

    [Fact]
    public void Positive_Int_WithPositive_ReturnsIt()
    {
        var result = Guard.Positive(3, "test");
        result.Should().Be(3);
    }

    [Fact]
    public void Positive_Int_With0_ThrowsArgumentException()
    {
        var act = () => Guard.Positive(0, "test");
        act.Should().Throw<ArgumentException>().WithParameterName("test");
    }

    [Fact]
    public void NormalizeDate_ReturnsDateTimeKindUnspecified()
    {
        var date = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var result = Guard.NormalizeDate(date);
        result.Kind.Should().Be(DateTimeKind.Unspecified);
        result.Should().Be(new DateTime(2025, 6, 15, 10, 30, 0));
    }
}
