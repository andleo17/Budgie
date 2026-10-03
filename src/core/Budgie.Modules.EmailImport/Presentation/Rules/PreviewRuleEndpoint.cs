using Budgie.Modules.EmailImport.Application.Rules.PreviewRule;
using Budgie.Shared.Enums;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.EmailImport.Presentation.Rules;

/// <summary>
/// The rule tester (<c>POST /api/email-import/rules/preview</c>): runs the full parse
/// pipeline against a saved rule (<c>ruleId</c>) or a rule being edited (<c>draft</c>) and
/// reports the plain text, the extracted fields and the per-field confidence — without
/// writing anything to the database (spec §7). What makes rules writable in practice:
/// without it the user would craft regexes blind and wait for real bank emails to find out.
/// </summary>
public static class PreviewRuleEndpoint
{
    public static void MapEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/rules/preview", async (PreviewRuleRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(req.ToCommand());
            return result.Match(
                value => Results.Ok(value),
                errors => Results.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
        });
    }
}

/// <summary>
/// Body of <c>POST /api/email-import/rules/preview</c>. Either <see cref="RuleId"/>
/// (test a saved rule) or <see cref="Draft"/> (test a rule being written, not persisted) —
/// the handler rejects a body with neither.
/// </summary>
public record PreviewRuleRequest(
    Guid? RuleId,
    RuleDraftRequest? Draft,
    string RawEmail,
    string? FromAddress,
    string? Subject)
{
    internal PreviewRuleCommand ToCommand() => new(
        RuleId, Draft?.ToDraft(), RawEmail, FromAddress, Subject);
}

/// <summary>
/// User-editable shape of a rule being written, mirroring Application's
/// <see cref="RuleDraft"/> field for field. Kept as a separate Presentation record so the
/// wire contract does not leak the Application type into clients.
/// </summary>
public record RuleDraftRequest(
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
    bool AutoCommit)
{
    internal RuleDraft ToDraft() => new(
        Name, AdapterKey, Priority, AccountId, CategoryId, TransactionType, Currency,
        ExchangeRate, FromAddress, SubjectContains, BodyRegex, FieldPatternsJson, AutoCommit);
}
