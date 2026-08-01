using Budgie.Modules.Finances.Domain.Currencies;
using Budgie.Shared.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Application.ExchangeRates;

public class CreateExchangeRateCommandHandlerTests
{
    [Fact]
    public void Handle_ShouldCreateExchangeRate()
    {
        var rate = ExchangeRate.Create("USD", "EUR", 0.85m, DateTime.UtcNow);

        rate.Id.Should().NotBeEmpty();
        rate.FromCurrency.Code.Should().Be("USD");
        rate.ToCurrency.Code.Should().Be("EUR");
        rate.Rate.Should().Be(0.85m);
    }

    [Fact]
    public void Handle_ZeroRate_ReturnsError()
    {
        var act = () => ExchangeRate.Create("USD", "EUR", 0m, DateTime.UtcNow);

        act.Should().Throw<ArgumentException>().WithMessage("Exchange rate must be positive.*");
    }

    [Fact]
    public void Handle_NegativeRate_ReturnsError()
    {
        var act = () => ExchangeRate.Create("USD", "EUR", -1m, DateTime.UtcNow);

        act.Should().Throw<ArgumentException>().WithMessage("Exchange rate must be positive.*");
    }

    [Fact]
    public void Handle_InvalidCurrency_ReturnsError()
    {
        var act = () => ExchangeRate.Create("INVALID", "EUR", 0.85m, DateTime.UtcNow);

        act.Should().Throw<ArgumentException>();
    }
}
