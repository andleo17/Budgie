using System.Text.RegularExpressions;

namespace Budgie.Shared.Domain.ValueObjects;

public sealed record HexColor
{
    private static readonly Regex ColorRegex = new(
        @"^#[0-9A-Fa-f]{6}$",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    public string Value { get; }

    private HexColor(string value) => Value = value.ToUpperInvariant();

    public static HexColor From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Color cannot be null or empty.", nameof(value));
        value = value.Trim();
        if (!ColorRegex.IsMatch(value))
            throw new ArgumentException("Color must be in #RRGGBB format.", nameof(value));
        return new HexColor(value);
    }

    public static HexColor? FromNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return From(value);
    }

    public static HexColor? TryFrom(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        if (!ColorRegex.IsMatch(value)) return null;
        return new HexColor(value);
    }

    public static implicit operator string?(HexColor? c) => c?.Value;

    public override string ToString() => Value;
}
