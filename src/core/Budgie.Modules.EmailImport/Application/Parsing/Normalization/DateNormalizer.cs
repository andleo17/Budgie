using System.Globalization;
using System.Text.RegularExpressions;

namespace Budgie.Modules.EmailImport.Application.Parsing.Normalization;

/// <summary>
/// Language-aware date normalization for the formats found in bank notification emails:
/// <c>dd/MM/yyyy</c>, <c>dd-MM-yyyy</c>, ISO <c>yyyy-MM-dd</c>, and textual month names in
/// Spanish and English in both orders: day-first (<c>2 oct 2026</c>, <c>2 de octubre de
/// 2026</c>) and month-first (<c>October 2, 2026</c>, <c>octubre 2 de 2026</c>).
/// Abbreviations accept an optional trailing dot: <c>2 oct. 2026</c>, <c>Oct. 2, 2026</c>.
/// </summary>
/// <remarks>
/// Always returns <see cref="DateTimeKind.Unspecified"/>, matching
/// <c>Budgie.Shared.Abstractions.Guard.NormalizeDate</c> and <c>Transaction.GuardDate</c>:
/// the staging column is <c>TIMESTAMP WITHOUT TIME ZONE</c> and the value must carry no
/// implicit zone.
/// </remarks>
public static partial class DateNormalizer
{
    /// <summary>
    /// Attempts to parse <paramref name="raw"/> into a calendar date.
    /// </summary>
    /// <param name="raw">Raw text captured from the email.</param>
    /// <param name="languageCode">
    /// Two-letter user language. It resolves the ambiguous numeric formats with the year
    /// last: <c>01/02/2026</c> is 1 February in <c>es</c> (day first) and 2 January in
    /// <c>en</c> (month first). When the language-preferred order is invalid
    /// (<c>25/12/2026</c> under <c>en</c>) the other order is tried before giving up.
    /// Any other value is treated as <c>en</c>.
    /// </param>
    /// <param name="value">The parsed date with <see cref="DateTimeKind.Unspecified"/>, when this method returns true.</param>
    /// <returns>
    /// False for empty input, unparseable text, or impossible dates (January 32nd,
    /// February 30th, month 13 when no reading makes it valid). Never throws.
    /// </returns>
    /// <remarks>
    /// Deliberate consequence of the numeric fallback: a date whose language-preferred
    /// reading is impossible is reinterpreted with the swapped order instead of rejected —
    /// <c>01/13/2026</c> parses as 13 January under both <c>es</c> and <c>en</c> rather
    /// than failing as "month 13". That is a conscious trade-off, not a bug: the same
    /// fallback is what makes <c>25/12/2026</c> work for an English-locale user reading a
    /// Spanish bank email, which is a far more common real-world case. Dates impossible
    /// under <i>both</i> orders are still rejected (<c>13/13/2026</c>, <c>32/01/2026</c>,
    /// <c>October 32, 2026</c>).
    /// </remarks>
    public static bool TryParse(string raw, string languageCode, out DateTime value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        var s = raw.Trim();

        // ISO-like: the 4-digit year comes first, so the reading is language-independent.
        var iso = IsoDateRegex().Match(s);
        if (iso.Success)
            return TryCompose(
                int.Parse(iso.Groups["year"].Value, CultureInfo.InvariantCulture),
                int.Parse(iso.Groups["month"].Value, CultureInfo.InvariantCulture),
                int.Parse(iso.Groups["day"].Value, CultureInfo.InvariantCulture),
                out value);

        // Numeric with the year last: the language decides which of the first two
        // components is the day and which is the month.
        var numeric = NumericDateRegex().Match(s);
        if (numeric.Success)
        {
            var a = int.Parse(numeric.Groups["a"].Value, CultureInfo.InvariantCulture);
            var b = int.Parse(numeric.Groups["b"].Value, CultureInfo.InvariantCulture);
            var year = int.Parse(numeric.Groups["year"].Value, CultureInfo.InvariantCulture);
            var isSpanish = IsSpanish(languageCode);

            var firstIsDay = isSpanish;
            if (TryCompose(year, firstIsDay ? b : a, firstIsDay ? a : b, out value))
                return true;

            // Language-preferred order invalid: try the other one. This keeps dd/MM emails
            // working for English-locale users (25/12/2026) without inventing dates.
            return TryCompose(year, firstIsDay ? a : b, firstIsDay ? b : a, out value);
        }

        // Month names, in Spanish or English, day-first — the Spanish default — with an
        // optional "de" filler ("2 oct 2026", "2 de octubre de 2026", "2 oct. 2026").
        var named = MonthNameDateRegex().Match(s);
        if (named.Success &&
            TryGetMonth(named.Groups["month"].Value, out var month))
        {
            return TryCompose(
                int.Parse(named.Groups["year"].Value, CultureInfo.InvariantCulture),
                month,
                int.Parse(named.Groups["day"].Value, CultureInfo.InvariantCulture),
                out value);
        }

        // Month-first — the English default ("October 2, 2026", "Oct 2, 2026") — also
        // accepted in Spanish ("octubre 2, 2026", "octubre 2 de 2026"). Day-first was
        // tried first; a month name can never be a day number, so the two branches cannot
        // conflict. Impossible dates are still rejected here, never reinterpreted.
        var monthFirst = MonthFirstDateRegex().Match(s);
        if (monthFirst.Success &&
            TryGetMonth(monthFirst.Groups["month"].Value, out var monthFirstNumber))
        {
            return TryCompose(
                int.Parse(monthFirst.Groups["year"].Value, CultureInfo.InvariantCulture),
                monthFirstNumber,
                int.Parse(monthFirst.Groups["day"].Value, CultureInfo.InvariantCulture),
                out value);
        }

        return false;
    }

    private static bool IsSpanish(string? languageCode) =>
        !string.IsNullOrEmpty(languageCode) &&
        languageCode.StartsWith("es", StringComparison.OrdinalIgnoreCase);

    /// <summary>Validates the parts and builds the date as <see cref="DateTimeKind.Unspecified"/>.</summary>
    private static bool TryCompose(int year, int month, int day, out DateTime value)
    {
        value = default;
        if (year is < 1 or > 9999 || month is < 1 or > 12 || day < 1)
            return false;
        if (day > DateTime.DaysInMonth(year, month))
            return false; // rejects January 32nd and February 30th
        value = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);
        return true;
    }

    /// <summary>Maps a Spanish or English month name (or abbreviation) to its 1-based number.</summary>
    private static bool TryGetMonth(string token, out int month)
    {
        // Trim a trailing abbreviation dot ("ene." → "ene") and normalize case.
        var key = token.Trim().TrimEnd('.').ToLowerInvariant();
        return Months.TryGetValue(key, out month);
    }

    private static readonly Dictionary<string, int> Months = new(StringComparer.Ordinal)
    {
        // English
        ["jan"] = 1, ["january"] = 1,
        ["feb"] = 2, ["february"] = 2,
        ["mar"] = 3, ["march"] = 3,
        ["apr"] = 4, ["april"] = 4,
        ["may"] = 5,
        ["jun"] = 6, ["june"] = 6,
        ["jul"] = 7, ["july"] = 7,
        ["aug"] = 8, ["august"] = 8,
        ["sep"] = 9, ["sept"] = 9, ["september"] = 9,
        ["oct"] = 10, ["october"] = 10,
        ["nov"] = 11, ["november"] = 11,
        ["dec"] = 12, ["december"] = 12,
        // Spanish (shared abbreviations appear once)
        ["ene"] = 1, ["enero"] = 1,
        ["febrero"] = 2,
        ["marzo"] = 3,
        ["abr"] = 4, ["abril"] = 4,
        ["mayo"] = 5,
        ["junio"] = 6,
        ["julio"] = 7,
        ["ago"] = 8, ["agosto"] = 8,
        ["set"] = 9, ["septiembre"] = 9, ["setiembre"] = 9,
        ["octubre"] = 10,
        ["noviembre"] = 11,
        ["dic"] = 12, ["diciembre"] = 12,
    };

    [GeneratedRegex(@"^(?<year>\d{4})(?<sep>[\/\-.])(?<month>\d{1,2})\k<sep>(?<day>\d{1,2})$")]
    private static partial Regex IsoDateRegex();

    [GeneratedRegex(@"^(?<a>\d{1,2})(?<sep>[\/\-.])(?<b>\d{1,2})\k<sep>(?<year>\d{4})$")]
    private static partial Regex NumericDateRegex();

    [GeneratedRegex(
        @"^(?<day>\d{1,2})[\s\-]+(?:de\s+)?(?<month>[a-zA-ZáéíóúñÁÉÍÓÚÑ]+)\.?\s+(?:de\s+)?(?<year>\d{4})$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MonthNameDateRegex();

    [GeneratedRegex(
        @"^(?<month>[a-zA-ZáéíóúñÁÉÍÓÚÑ]+)\.?\s+(?<day>\d{1,2})\s*,?\s*(?:de\s+)?(?<year>\d{4})$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MonthFirstDateRegex();
}
