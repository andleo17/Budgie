using Budgie.Modules.Finances.Application.RecurringTransactions.ApplyAllDueRecurringTransactions;
using Budgie.Modules.Finances.Application.RecurringTransactions.ApplyRecurringTransaction;
using Budgie.Modules.Finances.Application.RecurringTransactions.CreateRecurringTransaction;
using Budgie.Modules.Finances.Application.RecurringTransactions.DeleteRecurringTransaction;
using Budgie.Modules.Finances.Application.RecurringTransactions.GetDueRecurringTransactions;
using Budgie.Modules.Finances.Application.RecurringTransactions.GetRecurringTransaction;
using Budgie.Modules.Finances.Application.RecurringTransactions.GetRecurringTransactions;
using Budgie.Modules.Finances.Application.RecurringTransactions.Shared;
using Budgie.Modules.Finances.Application.RecurringTransactions.UpdateRecurringTransaction;
using Budgie.Shared.Dtos;
using Budgie.Shared.Enums;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Finances.Presentation.RecurringTransactions;

public static class RecurringTransactionEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("transactions/templates").WithTags("Recurring Transactions");

        group.MapPost("", async (CreateRecurringTransactionCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return result.Match<IResult>(
                id => TypedResults.Created($"/api/finances/transactions/templates/{id}", new { Id = id }),
                errors => TypedResults.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description }))
            );
        })
        .Produces<CreatedResponse>(StatusCodes.Status201Created);

        group.MapPut("{id:guid}", async (Guid id, UpdateRecurringTransactionCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command with { Id = id });
            return result.Match<IResult>(
                _ => TypedResults.NoContent(),
                errors => TypedResults.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description }))
            );
        })
        .Produces(StatusCodes.Status204NoContent);

        group.MapDelete("{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new DeleteRecurringTransactionCommand(id));
            return result.Match<IResult>(
                _ => TypedResults.NoContent(),
                errors => TypedResults.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description }))
            );
        })
        .Produces(StatusCodes.Status204NoContent);

        group.MapGet("", async (IMediator mediator, RecurringApplicability? applicability, bool? isActive) =>
        {
            var result = await mediator.Send(new GetRecurringTransactionsQuery(applicability, isActive));
            return result.Match<IResult>(
                templates => TypedResults.Ok(templates),
                errors => TypedResults.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description }))
            );
        })
        .Produces<List<RecurringTransactionResponse>>(StatusCodes.Status200OK);

        group.MapGet("{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetRecurringTransactionQuery(id));
            return result.Match<IResult>(
                template => TypedResults.Ok(template),
                errors => TypedResults.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description }))
            );
        })
        .Produces<RecurringTransactionResponse>(StatusCodes.Status200OK);

        group.MapPost("{id:guid}/apply", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new ApplyRecurringTransactionCommand(id));
            return result.Match<IResult>(
                transactionId => TypedResults.Ok(new { Id = transactionId }),
                errors => TypedResults.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description }))
            );
        })
        .Produces(StatusCodes.Status200OK);

        group.MapPost("apply-all-due", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new ApplyAllDueRecurringTransactionsCommand());
            return result.Match<IResult>(
                ids => TypedResults.Ok(new { AppliedIds = ids }),
                errors => TypedResults.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description }))
            );
        })
        .Produces(StatusCodes.Status200OK);

        group.MapGet("due", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetDueRecurringTransactionsQuery());
            return result.Match<IResult>(
                list => TypedResults.Ok(list),
                errors => TypedResults.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description }))
            );
        })
        .Produces<List<DueRecurringResponse>>(StatusCodes.Status200OK);
    }
}