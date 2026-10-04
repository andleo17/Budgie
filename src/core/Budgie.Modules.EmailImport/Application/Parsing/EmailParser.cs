using Budgie.Modules.EmailImport.Application.Abstractions;
using Budgie.Modules.EmailImport.Domain.Parsing;
using Budgie.Modules.EmailImport.Domain.Rules;

namespace Budgie.Modules.EmailImport.Application.Parsing;

/// <summary>
/// Parses one email against the user's import rules: extracts the plain text once, picks the
/// first enabled rule whose filters match <b>and which can own the message</b> (spec §4.3) and
/// delegates to the rule's adapter.
/// </summary>
/// <remarks>
/// A rule whose filters match can still refuse the message — a captured currency symbol that
/// contradicts the rule's currency is a hard fact, not a low score (§2.3 option (c)). Such a
/// rule is SKIPPED and the next candidate is tried, so the first matching filter does not
/// necessarily win.
/// </remarks>
public interface IEmailParser
{
    /// <summary>
    /// Parses <paramref name="message"/> with the active rules. The plain text is extracted
    /// exactly once and reused for every rule. When nothing can own the message the outcome is
    /// <see cref="ParseOutcome.NoMatch(string)"/> — with reason <c>no-rule-matched</c> if no filter
    /// matched at all, or the rejection reason (e.g. <c>currency-symbol-contradiction</c>) if the
    /// filters matched but every matching rule refused the message. Either way the message is
    /// logged and counted as skipped upstream, never persisted.
    /// </summary>
    /// <param name="message">The provider-neutral email.</param>
    /// <param name="rules">The user's rules; disabled ones are ignored.</param>
    /// <param name="languageCode">Two-letter user language (<c>es</c>/<c>en</c>) for normalization.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<ParseOutcome> ParseAsync(
        EmailMessage message,
        IReadOnlyList<EmailImportRule> rules,
        string languageCode,
        CancellationToken ct);
}

/// <inheritdoc cref="IEmailParser"/>
/// <remarks>
/// Scoped (not singleton) because it depends on <see cref="IMessageTextExtractor"/>, whose MIME
/// machinery is not guaranteed stateless. The adapter registry it delegates to is a singleton.
/// </remarks>
internal sealed class EmailParser(
    IMessageTextExtractor textExtractor,
    IBankAdapterRegistry registry) : IEmailParser
{
    public Task<ParseOutcome> ParseAsync(
        EmailMessage message,
        IReadOnlyList<EmailImportRule> rules,
        string languageCode,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(rules);
        ct.ThrowIfCancellationRequested();

        // ONE extraction per message, reused by every rule. Extracting per rule is the
        // difference that matters once a sync batch carries tens of messages: MIME decoding
        // and HTML conversion dominate the parse cost.
        var plainText = textExtractor.ExtractPlainText(message);

        // Disabled rules are filtered out before ordering. Priority ascending decides which
        // rule wins among those whose filters match (the first match wins), and Id breaks the
        // tie DETERMINISTICALLY (debt plan §6.8): two rules sharing a priority used to be
        // resolved by whatever order the database happened to return — invisible in the code and
        // observable only as "the same email imports differently". The loop below can now skip
        // a rule and try the next, which makes "which rule rejects first" observable too, so the
        // ordering has to be a decision rather than an accident of the query plan.
        //
        // THIS SORT IS AUTHORITATIVE. The repository orders by (Priority, Id) too so the rules
        // list is stable, but Postgres compares uuid as big-endian bytes and Guid.CompareTo
        // reads Data1 as a signed int, so the two orders are not guaranteed to agree on
        // same-priority rules. It does not matter: the parser never trusts the incoming order.
        var candidates = rules
            .Where(rule => rule.IsEnabled)
            .OrderBy(rule => rule.Priority)
            .ThenBy(rule => rule.Id);

        // The most recent rejection, so a message no rule could own is reported with the reason
        // that actually applied instead of the generic "no-rule-matched".
        string? rejectionReason = null;

        foreach (var rule in candidates)
        {
            // MatchesFilters (domain) evaluates FromAddress / SubjectContains / BodyRegex
            // with AND; the selection logic itself must not be reimplemented here.
            if (!rule.MatchesFilters(message.FromAddress, message.Subject, plainText))
                continue;

            var adapter = registry.Get(rule.AdapterKey);
            var context = new EmailParseContext(message, plainText, rule, languageCode);
            var outcome = adapter.Parse(context);

            // RuleRejected is NOT terminal (debt plan §2.3): this rule's filters matched but a
            // hard fact contradicts it, so a LATER rule may still own the message. Returning
            // here would discard the email before the correct rule was ever tried — which is
            // worse than the defect it fixes.
            if (outcome.RuleRejected)
            {
                rejectionReason = outcome.SkipReason ?? rejectionReason;
                continue;
            }

            return Task.FromResult(outcome);
        }

        return Task.FromResult(
            rejectionReason is not null
                ? ParseOutcome.NoMatch(rejectionReason)
                : ParseOutcome.NoMatch("no-rule-matched"));
    }
}
