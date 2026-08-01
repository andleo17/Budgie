namespace Budgie.Shared.Domain.ValueObjects;

public sealed record CurrencyCode
{
    public string Code { get; }

    private CurrencyCode(string code) => Code = code;

    public static CurrencyCode From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Currency code cannot be null or empty.", nameof(value));
        value = value.Trim().ToUpperInvariant();
        if (value.Length != 3)
            throw new ArgumentException("Currency code must be exactly 3 characters.", nameof(value));
        return new CurrencyCode(value);
    }

    public static CurrencyCode? FromNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : From(value);

    public static implicit operator string(CurrencyCode c) => c.Code;

    public override string ToString() => Code;
}
