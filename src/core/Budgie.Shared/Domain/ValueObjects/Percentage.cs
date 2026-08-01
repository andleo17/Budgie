namespace Budgie.Shared.Domain.ValueObjects;

public sealed record Percentage
{
    public decimal Value { get; }

    private Percentage(decimal value) => Value = value;

    public static Percentage From(decimal value)
    {
        if (value < 0 || value > 100)
            throw new ArgumentException("Percentage must be between 0 and 100.", nameof(value));
        return new Percentage(value);
    }

    public static implicit operator decimal(Percentage p) => p.Value;

    public override string ToString() => $"{Value}%";
}
