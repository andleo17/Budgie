namespace Budgie.Shared.Domain.ValueObjects;

public sealed record Name
{
    public const int DefaultMaxLength = 100;

    public string Value { get; }

    private Name(string value) => Value = value;

    public static Name From(string value, int maxLength = DefaultMaxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Name cannot be null or empty.", nameof(value));
        value = value.Trim();
        if (value.Length > maxLength)
            throw new ArgumentException($"Name cannot exceed {maxLength} characters.", nameof(value));
        return new Name(value);
    }

    public static implicit operator string(Name n) => n.Value;

    public override string ToString() => Value;
}
