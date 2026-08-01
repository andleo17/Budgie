using Budgie.Shared.Domain.ValueObjects;
using FluentAssertions;

namespace Budgie.Shared.UnitTests.Domain.ValueObjects;

public sealed class HexColorTests
{
    [Fact]
    public void From_WithValidHex_ReturnsHexColor()
    {
        var color = HexColor.From("#FF0000");
        color.Value.Should().Be("#FF0000");
    }

    [Fact]
    public void From_WithLowercase_Uppercases()
    {
        var color = HexColor.From("#ffffff");
        color.Value.Should().Be("#FFFFFF");
    }

    [Fact]
    public void From_WithWhitespace_Trims()
    {
        var color = HexColor.From(" #abc123 ");
        color.Value.Should().Be("#ABC123");
    }

    [Fact]
    public void From_WithNull_ThrowsArgumentException()
    {
        var act = () => HexColor.From(null!);
        act.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Fact]
    public void From_WithEmpty_ThrowsArgumentException()
    {
        var act = () => HexColor.From("");
        act.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Fact]
    public void From_WithoutHash_ThrowsArgumentException()
    {
        var act = () => HexColor.From("FF0000");
        act.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Fact]
    public void From_WithShortFormat_ThrowsArgumentException()
    {
        var act = () => HexColor.From("#FFF");
        act.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Fact]
    public void From_WithInvalidHex_ThrowsArgumentException()
    {
        var act = () => HexColor.From("#GGGGGG");
        act.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Fact]
    public void FromNullable_WithNull_ReturnsNull()
    {
        var color = HexColor.FromNullable(null);
        color.Should().BeNull();
    }

    [Fact]
    public void FromNullable_WithValidHex_ReturnsHexColor()
    {
        var color = HexColor.FromNullable("#000000");
        color.Should().NotBeNull();
        color!.Value.Should().Be("#000000");
    }

    [Fact]
    public void Equality_SameColor_AreEqual()
    {
        var a = HexColor.From("#AABBCC");
        var b = HexColor.From("#AABBCC");
        a.Should().Be(b);
    }

    [Fact]
    public void ImplicitConversionToString_ReturnsValue()
    {
        var color = HexColor.From("#123456");
        string? result = color;
        result.Should().Be("#123456");
    }

    [Fact]
    public void FromNullable_WithEmpty_ReturnsNull()
    {
        var color = HexColor.FromNullable("");
        color.Should().BeNull();
    }

    [Fact]
    public void FromNullable_WithWhitespace_ReturnsNull()
    {
        var color = HexColor.FromNullable("   ");
        color.Should().BeNull();
    }

    [Fact]
    public void TryFrom_WithNull_ReturnsNull()
    {
        HexColor.TryFrom(null).Should().BeNull();
    }

    [Fact]
    public void TryFrom_WithEmpty_ReturnsNull()
    {
        HexColor.TryFrom("").Should().BeNull();
    }

    [Fact]
    public void TryFrom_WithValidHex_ReturnsHexColor()
    {
        var color = HexColor.TryFrom("#FF0000");
        color.Should().NotBeNull();
        color!.Value.Should().Be("#FF0000");
    }

    [Fact]
    public void TryFrom_WithShortFormat_ReturnsNull()
    {
        HexColor.TryFrom("#FFF").Should().BeNull();
    }

    [Fact]
    public void TryFrom_WithInvalidFormat_ReturnsNull()
    {
        HexColor.TryFrom("red").Should().BeNull();
    }

    [Fact]
    public void TryFrom_WithInvalidHex_ReturnsNull()
    {
        HexColor.TryFrom("#GGGGGG").Should().BeNull();
    }

    [Fact]
    public void TryFrom_Lowercase_Uppercases()
    {
        var color = HexColor.TryFrom("#ffffff");
        color.Should().NotBeNull();
        color!.Value.Should().Be("#FFFFFF");
    }
}
