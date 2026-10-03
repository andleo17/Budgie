using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.EmailImport.Application.Rules.PreviewRule;

/// <summary>
/// User-editable shape of a rule being written, before it exists. Lets the tester run the
/// full parse pipeline against a draft without persisting anything.
/// </summary>
/// <remarks>
/// No <c>Id</c> and no <c>IsEnabled</c>: a draft is not selectable at sync time yet, and the
/// preview always evaluates it as if enabled — the user is testing the patterns, not the flag.
/// </remarks>
public record RuleDraft(
    string Name,
    string AdapterKey,
    int Priority,
    Guid AccountId,
    Guid? CategoryId,
    TransactionType TransactionType,
    string Currency,
    decimal? ExchangeRate,
    string? FromAddress,
    string? SubjectContains,
    string? BodyRegex,
    string? FieldPatternsJson,
    bool AutoCommit);

/// <summary>
/// Runs the parse pipeline over one email with one rule — saved (<see cref="RuleId"/>) or
/// being edited (<see cref="Draft"/>) — and reports what the adapter extracted, without
/// writing anything to the database.
/// </summary>
/// <remarks>
/// This is what makes rules writable in practice: without it the user would craft regexes
/// blind, save, and wait for real bank emails to discover mistakes. The response carries the
/// plain text the converter produced, because that text — not the email source the user
/// pasted — is what the regexes actually match against, and seeing it is how a user
/// discovers the HTML converter mishandled a particular bank format.
/// </remarks>
/// <param name="RuleId">Saved rule to test. Takes precedence over <paramref name="Draft"/>.</param>
/// <param name="Draft">Rule being edited (not persisted). Used when <paramref name="RuleId"/> is null.</param>
/// <param name="RawEmail">Email body as the user pasted it: loose text, HTML, or raw MIME source.</param>
/// <param name="FromAddress">Sender, to exercise the rule's FromAddress filter semantics downstream.</param>
/// <param name="Subject">Subject, likewise for the SubjectContains filter.</param>
public record PreviewRuleCommand(
    Guid? RuleId,
    RuleDraft? Draft,
    string RawEmail,
    string? FromAddress,
    string? Subject) : IRequest<ErrorOr<PreviewRuleResponse>>;

/// <summary>
/// Result of a rule preview: the plain text produced from the pasted email plus everything
/// the adapter extracted from it, with per-field confidence and the human-readable
/// explanation of each score.
/// </summary>
/// <param name="PlainText">
/// Plain-text rendering of <c>RawEmail</c> after MIME/HTML conversion. The regexes matched
/// against this, so it is always returned — including when nothing matched — to debug the
/// conversion itself.
/// </param>
/// <param name="Fields">
/// Extracted values keyed by field (<c>amount</c>, <c>currency</c>, <c>date</c>, <c>merchant</c>,
/// <c>type</c>); empty when the email was skipped.
/// </param>
/// <param name="FieldConfidence">Per-field scores (amount, direction, merchant, date, currency); empty when skipped.</param>
/// <param name="Confidence">Weighted total in [0, 1]; 0 when the email was skipped.</param>
/// <param name="Explanation">One line per field explaining its score; empty when skipped.</param>
/// <param name="SkipReason">Machine-readable reason when nothing was extracted (e.g. <c>amount-pattern-missing</c>); null on success.</param>
public record PreviewRuleResponse(
    string PlainText,
    IReadOnlyDictionary<string, string?> Fields,
    IReadOnlyDictionary<string, decimal> FieldConfidence,
    decimal Confidence,
    IReadOnlyList<string> Explanation,
    string? SkipReason);
