using Budgie.Modules.EmailImport.Application.Abstractions;
using Budgie.Modules.EmailImport.Domain.Parsing;
using Budgie.Modules.EmailImport.Domain.Rules;

namespace Budgie.Modules.EmailImport.Application.Parsing;

/// <summary>
/// Parses one email against the user's import rules: extracts the plain text once, picks the
/// first enabled rule whose filters match (spec §4.3) and delegates to the rule's adapter.
/// </summary>
public interface IEmailParser
{
    /// <summary>
    /// Parses <paramref name="message"/> with the active rules. The plain text is extracted
    /// exactly once and reused for every rule. When no rule matches, the outcome is
    /// <see cref="ParseOutcome.NoMatch(string)"/> with reason <c>no-rule-matched</c>: the
    /// message is logged and counted as skipped upstream, never persisted.
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

        // Disabled rules are filtered out before ordering; Priority ascending decides which
        // rule wins among those whose filters match (the first match wins).
        var candidates = rules
            .Where(rule => rule.IsEnabled)
            .OrderBy(rule => rule.Priority);

        foreach (var rule in candidates)
        {
            // MatchesFilters (domain) evaluates FromAddress / SubjectContains / BodyRegex
            // with AND; the selection logic itself must not be reimplemented here.
            if (!rule.MatchesFilters(message.FromAddress, message.Subject, plainText))
                continue;

            var adapter = registry.Get(rule.AdapterKey);
            var context = new EmailParseContext(message, plainText, rule, languageCode);
            return Task.FromResult(adapter.Parse(context));
        }

        return Task.FromResult(ParseOutcome.NoMatch("no-rule-matched"));
    }
}
