namespace Budgie.Shared.Abstractions;

public static class Guard
{
    public static string NotNullOrEmpty(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{paramName} cannot be null or empty.", paramName);
        return value;
    }

    public static T NotNull<T>(T? value, string paramName) where T : class
    {
        if (value is null)
            throw new ArgumentException($"{paramName} cannot be null.", paramName);
        return value;
    }

    public static decimal Positive(decimal value, string paramName)
    {
        if (value <= 0)
            throw new ArgumentException($"{paramName} must be positive.", paramName);
        return value;
    }

    public static decimal NonNegative(decimal value, string paramName)
    {
        if (value < 0)
            throw new ArgumentException($"{paramName} cannot be negative.", paramName);
        return value;
    }

    public static int Positive(int value, string paramName)
    {
        if (value <= 0)
            throw new ArgumentException($"{paramName} must be positive.", paramName);
        return value;
    }

    public static DateTime NormalizeDate(DateTime date)
    {
        return DateTime.SpecifyKind(date, DateTimeKind.Unspecified);
    }
}
