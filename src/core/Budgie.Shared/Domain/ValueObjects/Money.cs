namespace Budgie.Shared.Domain.ValueObjects;

public sealed record Money
{
    public decimal Amount { get; }
    public CurrencyCode Currency { get; }

    private Money(decimal amount, CurrencyCode currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Money FromPositive(decimal amount, CurrencyCode currency)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive.", nameof(amount));
        return new Money(amount, currency);
    }

    public static Money FromNonNegative(decimal amount, CurrencyCode currency)
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));
        return new Money(amount, currency);
    }

    public static Money FromAny(decimal amount, CurrencyCode currency) =>
        new(amount, currency);

    public static Money operator +(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new InvalidOperationException($"Cannot add money with different currencies: {a.Currency} and {b.Currency}.");
        return new Money(a.Amount + b.Amount, a.Currency);
    }

    public static Money operator -(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new InvalidOperationException($"Cannot subtract money with different currencies: {a.Currency} and {b.Currency}.");
        return new Money(a.Amount - b.Amount, a.Currency);
    }

    public static Money operator *(Money m, decimal factor) =>
        new(m.Amount * factor, m.Currency);

    public Money ConvertTo(CurrencyCode target, decimal exchangeRate)
    {
        if (exchangeRate <= 0)
            throw new ArgumentException("Exchange rate must be positive.", nameof(exchangeRate));
        return new Money(Amount * exchangeRate, target);
    }

    public bool IsSameCurrency(Money other) => Currency == other.Currency;

    public override string ToString() => $"{Amount:F2} {Currency.Code}";
}
