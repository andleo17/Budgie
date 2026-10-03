using Budgie.Modules.EmailImport.Application.Rules.CreateRule;
using Budgie.Modules.EmailImport.Application.Rules.DeleteRule;
using Budgie.Modules.EmailImport.Application.Rules.GetRules;
using Budgie.Modules.EmailImport.Application.Rules.UpdateRule;
using Budgie.Shared.Enums;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.EmailImport.Presentation.Rules;

/// <summary>
/// CRUD of import rules (list, create, update, delete). The rule tester lives in its own
/// endpoint (<see cref="PreviewRuleEndpoint"/>).
/// </summary>
internal static class RulesEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/rules", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetRulesQuery());
            return result.Match(
                value => Results.Ok(value),
                errors => Results.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
        });

        group.MapPost("/rules", async (CreateRuleRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(req.ToCommand());
            return result.Match(
                id => Results.Created($"/api/email-import/rules/{id}", new { id }),
                errors => Results.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
        });

        group.MapPut("/rules/{id:guid}", async (Guid id, UpdateRuleRequest req, IMediator mediator) =>
        {
            var result = await mediator.Send(req.ToCommand(id));
            return result.Match(
                _ => Results.NoContent(),
                errors => Results.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
        });

        group.MapDelete("/rules/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new DeleteRuleCommand(id));
            return result.Match(
                _ => Results.NoContent(),
                errors => Results.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
        });
    }
}

/// <summary>
/// Body of <c>POST /api/email-import/rules</c>. A Presentation-local record (never the
/// command itself): the wire contract is owned by the endpoint, so the command's shape can
/// evolve without breaking clients, and <c>UserId</c> is never bindable from the body —
/// it always comes from the authenticated user server-side.
/// </summary>
public record CreateRuleRequest(
    string Name,
    string AdapterKey,
    int Priority,
    bool IsEnabled,
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
    internal CreateRuleCommand ToCommand() => new(
        Name, AdapterKey, Priority, IsEnabled, AccountId, CategoryId, TransactionType,
        Currency, ExchangeRate, FromAddress, SubjectContains, BodyRegex, FieldPatternsJson,
        AutoCommit);
}

/// <summary>
/// Body of <c>PUT /api/email-import/rules/{id}</c>. Same shape as
/// <see cref="CreateRuleRequest"/>; the id travels in the route, never in the body.
/// </summary>
public record UpdateRuleRequest(
    string Name,
    string AdapterKey,
    int Priority,
    bool IsEnabled,
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
    internal UpdateRuleCommand ToCommand(Guid id) => new(
        id, Name, AdapterKey, Priority, IsEnabled, AccountId, CategoryId, TransactionType,
        Currency, ExchangeRate, FromAddress, SubjectContains, BodyRegex, FieldPatternsJson,
        AutoCommit);
}
