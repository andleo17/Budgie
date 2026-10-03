using Budgie.Modules.EmailImport.Domain.Rules;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;

namespace Budgie.Modules.EmailImport.Domain.Parsing;

/// <summary>
/// Transaction fields extracted from an email by a bank adapter, with per-field confidence.
/// Plain values (no Money/CurrencyCode) because this is parsing output, not a domain decision:
/// normalization and persistence happen downstream.
/// </summary>
/// <param name="Amount">Absolute amount extracted from the message.</param>
/// <param name="Currency">ISO 4217 code captured from the message or declared by the rule.</param>
/// <param name="Date">Operation date, <see cref="DateTimeKind.Unspecified"/>.</param>
/// <param name="Merchant">Merchant/payee, when the adapter could capture one.</param>
/// <param name="TransactionType">Income or Expense as resolved by the adapter.</param>
/// <param name="Confidence">Weighted total score in [0, 1].</param>
/// <param name="FieldConfidence">Per-field scores (amount, direction, merchant, date, currency).</param>
/// <param name="MultipleAmountCandidates">
/// True when the amount pattern matched more than once (e.g. previous balance / amount /
/// new balance); such messages are routed to review regardless of score.
/// </param>
public sealed record ExtractedTransaction(
    decimal Amount,
    string Currency,
    DateTime Date,
    string? Merchant,
    TransactionType TransactionType,
    decimal Confidence,
    IReadOnlyDictionary<string, decimal> FieldConfidence,
    bool MultipleAmountCandidates);

/// <summary>
/// Result of parsing one email with one rule: either a matched transaction or a documented
/// reason why nothing was extracted. Never carries a value without a match, nor a match
/// without a value — the factories enforce that invariant.
/// </summary>
/// <param name="Matched">Whether the adapter extracted a transaction.</param>
/// <param name="Value">The extracted transaction; null when <paramref name="Matched"/> is false.</param>
/// <param name="SkipReason">Machine-readable reason (e.g. <c>amount-not-found</c>); null on success.</param>
/// <param name="MatchedRule">
/// The rule that produced the match; null when nothing matched (or when the producer is an
/// adapter that does not report its rule). The sync service needs it to turn an extraction
/// into a <c>CreateTransactionCommand</c>: it carries the target <c>AccountId</c>,
/// <c>CategoryId</c>, <c>ExchangeRate</c> and the <c>AutoCommit</c> flag, and its Id is the
/// staging row's provenance. Added by slice 11; the parameter defaults to null so every
/// pre-existing call site keeps compiling unchanged.
/// </param>
public sealed record ParseOutcome(bool Matched, ExtractedTransaction? Value, string? SkipReason, EmailImportRule? MatchedRule = null)
{
    /// <summary>
    /// Creates a successful outcome without rule provenance. Prefer the overload that
    /// carries the rule — only legacy/test producers should need this one.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="tx"/> is null.</exception>
    public static ParseOutcome Success(ExtractedTransaction tx) =>
        new(true, Guard.NotNull(tx, nameof(tx)), null);

    /// <summary>
    /// Creates a successful outcome that also carries the rule that produced it.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="rule"/> or <paramref name="tx"/> is null.</exception>
    public static ParseOutcome Success(EmailImportRule rule, ExtractedTransaction tx) =>
        new(true, Guard.NotNull(tx, nameof(tx)), null, Guard.NotNull(rule, nameof(rule)));

    /// <summary>
    /// Creates a non-match outcome with the reason the email was skipped.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="skipReason"/> is null or empty.</exception>
    public static ParseOutcome NoMatch(string skipReason)
    {
        if (string.IsNullOrWhiteSpace(skipReason))
            throw new ArgumentException("SkipReason cannot be null or empty when there is no match.", nameof(skipReason));
        return new ParseOutcome(false, null, skipReason);
    }
}
