using Budgie.Modules.Finances.Application.Accounts;
using Budgie.Modules.Finances.Application.Accounts.DeleteAccount;
using Budgie.Modules.Finances.Application.Accounts.GetAccountById;
using Budgie.Modules.Finances.Application.Accounts.GetAccounts;
using Budgie.Modules.Finances.Application.Accounts.UpdateAccount;
using Budgie.Shared.Enums;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Finances.Presentation.Accounts;

internal static class AccountsEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/accounts", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetAccountsQuery());
            return result.Match(Results.Ok, err => Results.BadRequest(err));
        })
        .Produces<List<AccountResponse>>(StatusCodes.Status200OK);
        group.MapGet("/accounts/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetAccountByIdQuery(id));
            return result.Match(Results.Ok, err => Results.BadRequest(err));
        })
        .Produces<AccountResponse>(StatusCodes.Status200OK);
        group.MapPut("/accounts/{id:guid}", async (Guid id, UpdateAccountRequest req, IMediator mediator) =>
        {
            var cmd = new UpdateAccountCommand(
                id, req.Name, req.CreditLimit, req.CreditConsumptionMode,
                req.BillingCycleDay, req.PaymentDueDay, req.AnnualInterestRate,
                req.BillingCycleDayInclusive, req.InterestCalculationMethod,
                req.ControlStartDate);
            var result = await mediator.Send(cmd);
            return result.Match(_ => Results.NoContent(), err => Results.BadRequest(err));
        });
        group.MapDelete("/accounts/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new DeleteAccountCommand(id));
            return result.Match(_ => Results.NoContent(), err => Results.BadRequest(err));
        });
    }
}

public record UpdateAccountRequest(
    string Name, decimal? CreditLimit,
    CreditConsumptionMode? CreditConsumptionMode = null,
    int? BillingCycleDay = null, int? PaymentDueDay = null,
    decimal? AnnualInterestRate = null, bool BillingCycleDayInclusive = true,
    InterestCalculationMethod? InterestCalculationMethod = null,
    DateTime? ControlStartDate = null);
