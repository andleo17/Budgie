using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Budgie.Modules.EmailImport.Application.Parsing.Normalization;

/// <summary>
/// Language-aware amount normalization. Bank emails write the same number in opposite
/// conventions depending on the locale: <c>1.234,56</c> in Spanish, <c>1,234.56</c> in
/// English. The ambiguous case <c>1.234</c> (three digits behind the last separator) is
/// resolved by the language: thousands grouping in Spanish, decimal in English.
/// </summary>
/// <remarks>
/// Pure logic, no email-format knowledge: it belongs in Application because
/// <c>GenericRegexAdapter</c> consumes it directly. The adapter ignores the sign of the
/// result to decide the transaction type (the <c>direction</c> pattern or the rule does),
/// but a negative amount is still a strong signal it uses to boost the direction score.
/// </remarks>
public static partial class AmountNormalizer
{
    /// <summary>
    /// Attempts to parse <paramref name="raw"/> into a signed decimal amount.
    /// </summary>
    /// <param name="raw">
    /// Raw text captured from the email. Currency symbols (<c>€ $ £</c> and their HTML
    /// entities), whitespace — including NBSP <c> </c> and narrow no-break space
    /// <c> </c>, used by some emails as thousands separators — and stray letters are
    /// removed before parsing. A leading <c>-</c>/<c>+</c> sign is honored; the returned
    /// value keeps that sign.
    /// </param>
    /// <param name="languageCode">
    /// Two-letter user language: <c>es</c> (<c>,</c> decimal, <c>.</c> thousands) or
    /// <c>en</c> (<c>.</c> decimal, <c>,</c> thousands). Any other value is treated as
    /// <c>en</c>.
    /// </param>
    /// <param name="value">The parsed amount, with sign, when this method returns true.</param>
    /// <returns>
    /// True when <paramref name="raw"/> is a valid amount. False for empty input, non
    /// numeric text, a sign anywhere but the front, or more than two decimals in an
    /// unambiguous decimal reading. The single exception is the language-resolved ambiguous
    /// case (<c>1.234</c> in English → <c>1.234</c>), which is accepted as specified.
    /// </returns>
    public static bool TryParse(string raw, string languageCode, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        // 1. Strip currency symbols/entities, whitespace and any residual non-numeric noise
        //    (stray letters). What survives is digits, '.' , ',' and a possible sign.
        var s = NonNumericNoiseRegex().Replace(raw, string.Empty);
        if (s.Length == 0)
            return false;

        // 2. Sign: allowed only at the front. A '+'/'-' anywhere else means "not a number".
        var negative = false;
        if (s[0] is '-' or '+')
        {
            negative = s[0] == '-';
            s = s[1..];
        }
        if (s.Length == 0 || s.Contains('-') || s.Contains('+'))
            return false;

        var isSpanish = IsSpanish(languageCode);

        // 3. No separators: a plain integer.
        var lastDot = s.LastIndexOf('.');
        var lastComma = s.LastIndexOf(',');
        var lastSep = Math.Max(lastDot, lastComma);
        if (lastSep < 0)
            return TryCompose(s, negative, out value);

        // 4. The LAST separator and the digit count behind it decide what it means.
        var sepChar = s[lastSep];
        var decimals = CountDigitsAfter(s, lastSep);
        if (decimals == 0)
            return false; // trailing separator ("1.000,") is not a number

        if (lastDot >= 0 && lastComma >= 0)
        {
            // Both separators present: the later one is unambiguously the decimal separator,
            // the other one is thousands grouping. More than two decimals → reject.
            if (decimals > 2)
                return false;
            return TryCompose(NormalizeSeparators(s, sepChar), negative, out value);
        }

        // Single separator type.
        var occurrences = s.Count(c => c == sepChar);
        if (occurrences >= 2)
        {
            // Repeated separators of one type cannot be decimal; the last group decides.
            if (decimals > 3)
                return false; // forced decimal carrying more than 2 decimals
            if (decimals == 3)
            {
                // Grouping pattern ("1,234,567" / "1.234.567"): every separator is a
                // thousands separator in any language.
                return TryCompose(s.Replace(sepChar.ToString(), string.Empty), negative, out value);
            }
            // decimals is 1-2: the last occurrence is the decimal point, earlier ones are
            // thousands grouping ("1,234,56" → 1234.56).
            return TryCompose(MakeLastDecimal(s, sepChar), negative, out value);
        }

        if (decimals <= 2)
            return TryCompose(MakeLastDecimal(s, sepChar), negative, out value);

        if (decimals > 3)
            return false; // forced decimal carrying more than 2 decimals

        // decimals == 3 with a single separator: ambiguous by digit count alone. The
        // language decides — if the separator is that language's thousands separator the
        // value is an integer ("1.234"/es → 1234); otherwise it is a decimal
        // ("1.234"/en → 1.234). This is the case that breaks naive implementations.
        var sepIsThousandsForLanguage = isSpanish ? sepChar == '.' : sepChar == ',';
        return sepIsThousandsForLanguage
            ? TryCompose(s.Replace(sepChar.ToString(), string.Empty), negative, out value)
            : TryCompose(MakeLastDecimal(s, sepChar), negative, out value);
    }

    private static bool IsSpanish(string? languageCode) =>
        !string.IsNullOrEmpty(languageCode) &&
        languageCode.StartsWith("es", StringComparison.OrdinalIgnoreCase);

    private static bool TryCompose(string number, bool negative, out decimal value)
    {
        value = 0m;
        if (number.Length == 0)
            return false;
        if (!decimal.TryParse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed))
            return false;
        value = negative ? -parsed : parsed;
        return true;
    }

    /// <summary>Replaces the decimal separator with '.' (thousands separators are dropped).</summary>
    private static string NormalizeSeparators(string s, char decimalSep)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (c != decimalSep && c is '.' or ',')
                continue; // the other separator is thousands grouping
            sb.Append(c == decimalSep ? '.' : c);
        }
        return sb.ToString();
    }

    /// <summary>Makes the last occurrence of <paramref name="sep"/> the decimal point and drops the rest.</summary>
    private static string MakeLastDecimal(string s, char sep)
    {
        var idx = s.LastIndexOf(sep);
        var sb = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            if (i == idx)
                sb.Append('.');
            else if (s[i] != sep)
                sb.Append(s[i]);
        }
        return sb.ToString();
    }

    private static int CountDigitsAfter(string s, int sepIndex)
    {
        var count = 0;
        for (var i = sepIndex + 1; i < s.Length && char.IsAsciiDigit(s[i]); i++)
            count++;
        return count;
    }

    [GeneratedRegex(@"[^\d.,+\-]")]
    private static partial Regex NonNumericNoiseRegex();
}
