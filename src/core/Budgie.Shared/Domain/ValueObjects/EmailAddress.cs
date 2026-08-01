using System.Text.RegularExpressions;

namespace Budgie.Shared.Domain.ValueObjects;

public sealed record EmailAddress
{
    private static readonly Regex EmailRegex = new(
        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromMilliseconds(100));

    public string Value { get; }

    private EmailAddress(string value) => Value = value;

    public static EmailAddress From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Email cannot be null or empty.", nameof(value));
        value = value.Trim().ToLowerInvariant();
        if (!EmailRegex.IsMatch(value))
            throw new ArgumentException("Invalid email format.", nameof(value));
        return new EmailAddress(value);
    }

    public static implicit operator string(EmailAddress e) => e.Value;

    public override string ToString() => Value;
}
