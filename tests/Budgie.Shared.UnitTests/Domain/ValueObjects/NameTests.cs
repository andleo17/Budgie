using Budgie.Shared.Domain.ValueObjects;
using FluentAssertions;

namespace Budgie.Shared.UnitTests.Domain.ValueObjects;

public sealed class NameTests
{
    [Fact]
    public void From_WithValidName_ReturnsName()
    {
        var name = Name.From("John Doe");
        name.Value.Should().Be("John Doe");
    }

    [Fact]
    public void From_WithNull_ThrowsArgumentException()
    {
        var act = () => Name.From(null!);
        act.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Fact]
    public void From_WithEmpty_ThrowsArgumentException()
    {
        var act = () => Name.From("");
        act.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Fact]
    public void From_WithWhitespaceOnly_ThrowsArgumentException()
    {
        var act = () => Name.From("   ");
        act.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Fact]
    public void From_TrimsWhitespace()
    {
        var name = Name.From("  Jane Doe  ");
        name.Value.Should().Be("Jane Doe");
    }

    [Fact]
    public void From_ExceedsMaxLength_ThrowsArgumentException()
    {
        var act = () => Name.From(new string('a', 101));
        act.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Fact]
    public void From_CustomMaxLength_RespectsCustomLength()
    {
        var act = () => Name.From(new string('a', 6), maxLength: 5);
        act.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Fact]
    public void From_CustomMaxLength_WithValidShortName_ReturnsName()
    {
        var name = Name.From("Hello", maxLength: 5);
        name.Value.Should().Be("Hello");
    }

    [Fact]
    public void DefaultMaxLength_Is100()
    {
        Name.DefaultMaxLength.Should().Be(100);
    }

    [Fact]
    public void Equality_SameName_AreEqual()
    {
        var a = Name.From("Alice");
        var b = Name.From("Alice");
        a.Should().Be(b);
    }

    [Fact]
    public void ImplicitConversionToString_ReturnsValue()
    {
        var name = Name.From("Bob");
        string result = name;
        result.Should().Be("Bob");
    }
}
