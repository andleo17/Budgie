using Budgie.Shared.Domain.ValueObjects;

namespace Budgie.Modules.Finances.Domain.Currencies;

public class ExchangeRate
{
    public Guid Id { get; private set; }
    public CurrencyCode FromCurrency { get; private set; } = null!;
    public CurrencyCode ToCurrency { get; private set; } = null!;
    public decimal Rate { get; private set; }
    public DateTime Date { get; private set; }

    private ExchangeRate() { }

    public static ExchangeRate Create(string from, string to, decimal rate, DateTime date)
    {
        if (rate <= 0)
            throw new ArgumentException("Exchange rate must be positive.", nameof(rate));

        return new ExchangeRate
        {
            Id = Guid.NewGuid(),
            FromCurrency = CurrencyCode.From(from),
            ToCurrency = CurrencyCode.From(to),
            Rate = rate,
            Date = GuardDate(date)
        };
    }

    private static DateTime GuardDate(DateTime date) =>
        DateTime.SpecifyKind(date, DateTimeKind.Unspecified);
}
