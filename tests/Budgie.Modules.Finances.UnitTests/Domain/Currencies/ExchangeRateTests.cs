using Budgie.Modules.Finances.Domain.Currencies;
using FluentAssertions;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Domain.Currencies;

public class ExchangeRateTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var date = new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        var rate = ExchangeRate.Create("USD", "PEN", 3.75m, date);

        rate.Id.Should().NotBeEmpty();
        rate.FromCurrency.Code.Should().Be("USD");
        rate.ToCurrency.Code.Should().Be("PEN");
        rate.Rate.Should().Be(3.75m);
    }

    [Fact]
    public void Create_ShouldThrow_WhenRateIsZero()
    {
        var act = () => ExchangeRate.Create("USD", "PEN", 0m, DateTime.UtcNow);
        act.Should().Throw<ArgumentException>().WithMessage("Exchange rate must be positive.*");
    }

    [Fact]
    public void Create_ShouldThrow_WhenRateIsNegative()
    {
        var act = () => ExchangeRate.Create("USD", "PEN", -1m, DateTime.UtcNow);
        act.Should().Throw<ArgumentException>().WithMessage("Exchange rate must be positive.*");
    }

    [Fact]
    public void Create_ShouldUppercaseFromCurrency()
    {
        var rate = ExchangeRate.Create("usd", "PEN", 3.75m, DateTime.UtcNow);
        rate.FromCurrency.Code.Should().Be("USD");
    }

    [Fact]
    public void Create_ShouldUppercaseToCurrency()
    {
        var rate = ExchangeRate.Create("USD", "pen", 3.75m, DateTime.UtcNow);
        rate.ToCurrency.Code.Should().Be("PEN");
    }

    [Fact]
    public void Create_ShouldNormalizeDate()
    {
        var date = DateTime.UtcNow;
        var rate = ExchangeRate.Create("USD", "PEN", 3.75m, date);
        rate.Date.Kind.Should().Be(DateTimeKind.Unspecified);
    }
}
