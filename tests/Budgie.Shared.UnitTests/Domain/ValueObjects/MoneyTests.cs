using Budgie.Shared.Domain.ValueObjects;
using FluentAssertions;

namespace Budgie.Shared.UnitTests.Domain.ValueObjects;

public sealed class MoneyTests
{
    private static readonly CurrencyCode Usd = CurrencyCode.From("USD");
    private static readonly CurrencyCode Eur = CurrencyCode.From("EUR");

    [Fact]
    public void FromPositive_WithPositiveAmount_ReturnsMoney()
    {
        var money = Money.FromPositive(100m, Usd);
        money.Amount.Should().Be(100m);
        money.Currency.Should().Be(Usd);
    }

    [Fact]
    public void FromPositive_With0_ThrowsArgumentException()
    {
        var act = () => Money.FromPositive(0, Usd);
        act.Should().Throw<ArgumentException>().WithParameterName("amount");
    }

    [Fact]
    public void FromPositive_WithNegative_ThrowsArgumentException()
    {
        var act = () => Money.FromPositive(-1m, Usd);
        act.Should().Throw<ArgumentException>().WithParameterName("amount");
    }

    [Fact]
    public void FromNonNegative_With0_ReturnsMoney()
    {
        var money = Money.FromNonNegative(0, Usd);
        money.Amount.Should().Be(0);
    }

    [Fact]
    public void FromNonNegative_WithNegative_ThrowsArgumentException()
    {
        var act = () => Money.FromNonNegative(-0.01m, Usd);
        act.Should().Throw<ArgumentException>().WithParameterName("amount");
    }

    [Fact]
    public void FromAny_WithAnyDecimal_ReturnsMoney()
    {
        var money = Money.FromAny(-50m, Usd);
        money.Amount.Should().Be(-50m);
        money.Currency.Should().Be(Usd);
    }

    [Fact]
    public void OperatorPlus_SameCurrency_ReturnsSum()
    {
        var a = Money.FromAny(100m, Usd);
        var b = Money.FromAny(200m, Usd);
        var result = a + b;
        result.Amount.Should().Be(300m);
        result.Currency.Should().Be(Usd);
    }

    [Fact]
    public void OperatorPlus_DifferentCurrency_ThrowsInvalidOperationException()
    {
        var a = Money.FromAny(100m, Usd);
        var b = Money.FromAny(200m, Eur);
        var act = () => a + b;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void OperatorMinus_SameCurrency_ReturnsDifference()
    {
        var a = Money.FromAny(300m, Usd);
        var b = Money.FromAny(100m, Usd);
        var result = a - b;
        result.Amount.Should().Be(200m);
        result.Currency.Should().Be(Usd);
    }

    [Fact]
    public void OperatorMinus_DifferentCurrency_ThrowsInvalidOperationException()
    {
        var a = Money.FromAny(300m, Usd);
        var b = Money.FromAny(100m, Eur);
        var act = () => a - b;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void OperatorMultiply_WithFactor_ReturnsProduct()
    {
        var money = Money.FromAny(50m, Usd);
        var result = money * 2.5m;
        result.Amount.Should().Be(125m);
        result.Currency.Should().Be(Usd);
    }

    [Fact]
    public void ConvertTo_WithSameCurrency_ShouldReturnSameAmount()
    {
        var money = Money.FromAny(100m, Usd);
        var converted = money.ConvertTo(Usd, 1m);
        converted.Amount.Should().Be(100m);
        converted.Currency.Should().Be(Usd);
    }

    [Fact]
    public void ConvertTo_WithValidRate_ReturnsConvertedMoney()
    {
        var money = Money.FromAny(100m, Usd);
        var converted = money.ConvertTo(Eur, 0.85m);
        converted.Amount.Should().Be(85m);
        converted.Currency.Should().Be(Eur);
    }

    [Fact]
    public void ConvertTo_WithNegativeRate_ThrowsArgumentException()
    {
        var money = Money.FromAny(100m, Usd);
        var act = () => money.ConvertTo(Eur, -1m);
        act.Should().Throw<ArgumentException>().WithParameterName("exchangeRate");
    }

    [Fact]
    public void IsSameCurrency_WithSameCurrency_ReturnsTrue()
    {
        var a = Money.FromAny(10m, Usd);
        var b = Money.FromAny(20m, Usd);
        a.IsSameCurrency(b).Should().BeTrue();
    }

    [Fact]
    public void IsSameCurrency_WithDifferentCurrency_ReturnsFalse()
    {
        var a = Money.FromAny(10m, Usd);
        var b = Money.FromAny(10m, Eur);
        a.IsSameCurrency(b).Should().BeFalse();
    }

    [Fact]
    public void Equality_SameAmountAndCurrency_AreEqual()
    {
        var a = Money.FromAny(100m, Usd);
        var b = Money.FromAny(100m, Usd);
        a.Should().Be(b);
    }

    [Fact]
    public void Equality_DifferentCurrency_AreNotEqual()
    {
        var a = Money.FromAny(100m, Usd);
        var b = Money.FromAny(100m, Eur);
        a.Should().NotBe(b);
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        var money = Money.FromAny(123.456m, Usd);
        money.ToString().Should().Be("123.46 USD");
    }
}
