using System.Globalization;

namespace Budgie.Modules.EmailImport.Application.Parsing;

/// <summary>
/// Turns per-field scores (0..1 each) into the weighted total confidence of a parse:
/// <c>Confidence = Σ (weight × score)</c> using the constants in <see cref="ConfidenceWeights"/>.
/// A field absent from the dictionary counts as 0 — an unscored field must cost its weight,
/// never be silently ignored.
/// </summary>
public static class ConfidenceScorer
{
    /// <summary>Field key of the extracted amount.</summary>
    public const string AmountField = "amount";

    /// <summary>Field key of the transaction direction.</summary>
    public const string DirectionField = "direction";

    /// <summary>Field key of the merchant/payee.</summary>
    public const string MerchantField = "merchant";

    /// <summary>Field key of the operation date.</summary>
    public const string DateField = "date";

    /// <summary>Field key of the currency check.</summary>
    public const string CurrencyField = "currency";

    /// <summary>
    /// Computes the weighted total confidence. Scores outside [0, 1] are clamped: the scorer
    /// never lets a buggy adapter inflate the confidence past 1.
    /// </summary>
    /// <param name="fieldScores">Per-field scores keyed by the <c>*Field</c> constants.</param>
    public static decimal Compute(IReadOnlyDictionary<string, decimal> fieldScores)
    {
        ArgumentNullException.ThrowIfNull(fieldScores);

        return ConfidenceWeights.Amount * ScoreOf(fieldScores, AmountField)
             + ConfidenceWeights.Direction * ScoreOf(fieldScores, DirectionField)
             + ConfidenceWeights.Merchant * ScoreOf(fieldScores, MerchantField)
             + ConfidenceWeights.Date * ScoreOf(fieldScores, DateField)
             + ConfidenceWeights.Currency * ScoreOf(fieldScores, CurrencyField);
    }

    /// <summary>
    /// Human-readable, per-field breakdown of the scores, in a stable field order
    /// (amount, direction, merchant, date, currency). This is what the review UI shows and
    /// what tests assert on: a bare number cannot explain why an email went to review.
    /// </summary>
    /// <param name="fieldScores">Per-field scores keyed by the <c>*Field</c> constants.</param>
    /// <param name="languageCode">Two-letter user language: <c>es</c> or <c>en</c>.</param>
    /// <returns>One line per field, formatted as <c>field: 0.00 — reason</c>.</returns>
    public static IReadOnlyList<string> Explain(IReadOnlyDictionary<string, decimal> fieldScores, string languageCode)
    {
        ArgumentNullException.ThrowIfNull(fieldScores);
        var spanish = !string.IsNullOrEmpty(languageCode) &&
                      languageCode.StartsWith("es", StringComparison.OrdinalIgnoreCase);

        return new List<string>(5)
        {
            FieldLine(fieldScores, AmountField, spanish, AmountReason),
            FieldLine(fieldScores, DirectionField, spanish, DirectionReason),
            FieldLine(fieldScores, MerchantField, spanish, MerchantReason),
            FieldLine(fieldScores, DateField, spanish, DateReason),
            FieldLine(fieldScores, CurrencyField, spanish, CurrencyReason),
        };
    }

    private static decimal ScoreOf(IReadOnlyDictionary<string, decimal> fieldScores, string field)
    {
        if (!fieldScores.TryGetValue(field, out var score))
            return 0m;
        return Math.Clamp(score, 0m, 1m);
    }

    private static string FieldLine(
        IReadOnlyDictionary<string, decimal> fieldScores,
        string field,
        bool spanish,
        Func<decimal, bool, string> reasonFor)
    {
        if (!fieldScores.TryGetValue(field, out var score))
            return $"{field}: 0.00 — {(spanish ? "campo sin puntuar" : "field not scored")}";

        score = Math.Clamp(score, 0m, 1m);
        var reason = reasonFor(score, spanish);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{field}: {score.ToString("0.00", CultureInfo.InvariantCulture)} — {reason}");
    }

    // The bands below map the discrete scores the adapters produce (1.0 / 0.5 / 0.3 / 0.0)
    // onto a readable reason. Ranges are used instead of equality so a partially-scored custom
    // dictionary still explains sensibly.

    private static string AmountReason(decimal score, bool spanish)
    {
        if (score >= 0.999m)
            return spanish ? "importe extraído y normalizado" : "amount extracted and normalized";
        if (score >= 0.4m)
            return spanish
                ? "dos coincidencias del patrón de importe (mensaje ambiguo)"
                : "two amount-pattern matches (ambiguous message)";
        if (score >= 0.25m)
            return spanish
                ? "tres o más coincidencias del patrón de importe (mensaje ambiguo)"
                : "three or more amount-pattern matches (ambiguous message)";
        return spanish
            ? "el texto capturado no es un importe válido"
            : "captured text is not a valid amount";
    }

    private static string DirectionReason(decimal score, bool spanish)
    {
        return score >= 0.999m
            ? (spanish
                ? "dirección determinada por el patrón o fijada por la regla"
                : "direction resolved by pattern or fixed by the rule")
            : (spanish
                ? "patrón de dirección ambiguo; se mantiene el tipo de la regla"
                : "ambiguous direction pattern; rule type kept");
    }

    private static string MerchantReason(decimal score, bool spanish)
    {
        if (score >= 0.999m)
            return spanish ? "comercio extraído del mensaje" : "merchant extracted from the message";
        if (score >= 0.25m)
            return spanish
                ? "comercio genérico del banco (boilerplate)"
                : "generic bank wording (boilerplate)";
        return spanish ? "sin comercio capturado" : "no merchant captured";
    }

    private static string DateReason(decimal score, bool spanish)
    {
        if (score >= 0.999m)
            return spanish ? "fecha extraída y no futura" : "date extracted and not in the future";
        if (score >= 0.4m)
            return spanish
                ? "varias fechas distintas en el mensaje, o fecha de recepción del correo"
                : "several distinct dates in the message, or email receipt date";
        return spanish
            ? "fecha no parseable o futura"
            : "date unparseable or in the future";
    }

    private static string CurrencyReason(decimal score, bool spanish)
    {
        return score >= 0.999m
            ? (spanish
                ? "moneda coherente con la regla"
                : "currency consistent with the rule")
            : (spanish
                ? "el símbolo capturado contradice la moneda de la regla"
                : "captured symbol contradicts the rule currency");
    }
}
