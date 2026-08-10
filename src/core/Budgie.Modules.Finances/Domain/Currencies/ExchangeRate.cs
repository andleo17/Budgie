using Budgie.Shared.Domain.ValueObjects;

namespace Budgie.Modules.Finances.Domain.Currencies;

public class ExchangeRate
{
    public Guid Id { get; internal set; }
    public CurrencyCode FromCurrency { get; internal set; } = null!;
    public CurrencyCode ToCurrency { get; internal set; } = null!;
    public decimal Rate { get; internal set; }
    public DateTime Date { get; internal set; }

    public ExchangeRate() { }

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
