using Budgie.Modules.Finances.Application.Accounts.CreateAccount;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Finances.Presentation.Accounts;

public static class CreateAccountEndpoint
{
    public static void MapEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/accounts", async (CreateAccountRequest req, IMediator mediator) =>
        {
            var cmd = new CreateAccountCommand(
                req.Name, req.Currency, req.InitialBalance,
                (AccountType)(req.Type ?? 1),
                req.CreditLimit, req.CreditConsumptionMode, req.BillingCycleDay,
                req.PaymentDueDay, req.AnnualInterestRate, req.BillingCycleDayInclusive,
                req.InterestCalculationMethod, req.CreatedAt);
            var result = await mediator.Send(cmd);
            return result.Match(
                id => Results.Created($"/api/finances/accounts/{id}", new { id }),
                errors => Results.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
        });
    }
}

public record CreateAccountRequest(
    string Name, string Currency, decimal InitialBalance, int? Type, decimal? CreditLimit,
    CreditConsumptionMode? CreditConsumptionMode = null, int? BillingCycleDay = null,
    int? PaymentDueDay = null, decimal? AnnualInterestRate = null,
    bool BillingCycleDayInclusive = true, InterestCalculationMethod? InterestCalculationMethod = null,
    DateTime? CreatedAt = null);
