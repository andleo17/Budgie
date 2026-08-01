using Budgie.Shared.Domain.ValueObjects;
using FluentAssertions;

namespace Budgie.Shared.UnitTests.Domain.ValueObjects;

public sealed class EmailAddressTests
{
    [Fact]
    public void From_WithSimpleEmail_ReturnsEmailAddress()
    {
        var email = EmailAddress.From("user@example.com");
        email.Value.Should().Be("user@example.com");
    }

    [Fact]
    public void From_WithDots_ReturnsEmailAddress()
    {
        var email = EmailAddress.From("first.last@example.co.uk");
        email.Value.Should().Be("first.last@example.co.uk");
    }

    [Fact]
    public void From_WithPlus_ReturnsEmailAddress()
    {
        var email = EmailAddress.From("user+tag@example.com");
        email.Value.Should().Be("user+tag@example.com");
    }

    [Fact]
    public void From_WithNull_ThrowsArgumentException()
    {
        var act = () => EmailAddress.From(null!);
        act.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Fact]
    public void From_WithEmpty_ThrowsArgumentException()
    {
        var act = () => EmailAddress.From("");
        act.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Fact]
    public void From_WithNoAt_ThrowsArgumentException()
    {
        var act = () => EmailAddress.From("userexample.com");
        act.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Fact]
    public void From_WithNoDomain_ThrowsArgumentException()
    {
        var act = () => EmailAddress.From("user@");
        act.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Fact]
    public void From_WithSpecialChars_ThrowsArgumentException()
    {
        var act = () => EmailAddress.From("user@ex ample.com");
        act.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Fact]
    public void From_NormalizesToLowercase()
    {
        var email = EmailAddress.From("USER@Example.COM");
        email.Value.Should().Be("user@example.com");
    }

    [Fact]
    public void From_TrimsWhitespace()
    {
        var email = EmailAddress.From("  user@example.com  ");
        email.Value.Should().Be("user@example.com");
    }

    [Fact]
    public void Equality_SameEmail_AreEqual()
    {
        var a = EmailAddress.From("user@example.com");
        var b = EmailAddress.From("user@example.com");
        a.Should().Be(b);
    }

    [Fact]
    public void ImplicitConversionToString_ReturnsValue()
    {
        var email = EmailAddress.From("test@example.com");
        string result = email;
        result.Should().Be("test@example.com");
    }
}
