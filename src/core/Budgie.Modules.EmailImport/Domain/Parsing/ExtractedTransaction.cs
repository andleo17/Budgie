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
/// <remarks>
/// There are <b>three</b> outcomes, not two, and the third one is not a flavour of the second:
/// <list type="number">
/// <item><description>
/// <see cref="Success(ExtractedTransaction)"/> — a transaction was extracted.
/// </description></item>
/// <item><description>
/// <see cref="RuleCannotOwn(string)"/> — the rule's FILTERS matched this email, but a hard fact
/// about the message contradicts the rule, so <b>this rule cannot own it</b>. The parser keeps
/// trying the remaining rules: the next one may be the right owner. A contradicting currency
/// symbol is the case that exists today (debt plan §2.3, option (c)) — the amount is right but
/// labelling it EUR when the email says dollars is the one way this system can file money under
/// the wrong currency without a human looking at it.
/// </description></item>
/// <item><description>
/// <see cref="NoMatch(string)"/> — nothing was extracted, for any reason. Terminal.
/// </description></item>
/// </list>
/// Treating <see cref="RuleRejected"/> as a plain <see cref="NoMatch(string)"/> would be a
/// regression, not a simplification: <c>EmailParser</c> returns the first rule whose filters
/// match, so a rejected email would be discarded before a later, correct rule was ever tried.
/// </remarks>
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
/// <param name="RuleRejected">
/// True when the outcome is a "this rule cannot own this email" verdict rather than a terminal
/// no-match. Defaults to false so every existing producer and call site keeps compiling
/// unchanged; build it with <see cref="RuleCannotOwn(string)"/>, never by hand.
/// <para>
/// <b>This flag is a signal BETWEEN an adapter and the parser, never part of the parser's
/// result.</b> <c>EmailParser</c> consumes it while looping over rules and always returns a
/// terminal outcome, so code that calls <c>ParseAsync</c> will never see it set — a rejected
/// email surfaces as a <see cref="NoMatch"/> carrying the same reason. Reading this flag on the
/// result of a parse is therefore a bug that compiles and silently never fires.
/// </para>
/// </param>
public sealed record ParseOutcome(
    bool Matched,
    ExtractedTransaction? Value,
    string? SkipReason,
    EmailImportRule? MatchedRule = null,
    bool RuleRejected = false)
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
    /// Creates a non-match outcome with the reason the email was skipped. Terminal: the parser
    /// stops here.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="skipReason"/> is null or empty.</exception>
    public static ParseOutcome NoMatch(string skipReason)
    {
        if (string.IsNullOrWhiteSpace(skipReason))
            throw new ArgumentException("SkipReason cannot be null or empty when there is no match.", nameof(skipReason));
        return new ParseOutcome(false, null, skipReason);
    }

    /// <summary>
    /// Creates a non-terminal outcome: the rule's filters matched but a hard fact contradicts
    /// the rule, so this rule cannot own the email. The parser continues with the next rule and
    /// only falls back to a terminal <see cref="NoMatch"/> with this same reason if no remaining
    /// rule can own it either. Named for what it asserts rather than for the flag it sets
    /// (<see cref="RuleRejected"/>), so the two never read as the same concept.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="reason"/> is null or empty.</exception>
    public static ParseOutcome RuleCannotOwn(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A rejection must carry a reason.", nameof(reason));
        return new ParseOutcome(false, null, reason, null, RuleRejected: true);
    }
}
