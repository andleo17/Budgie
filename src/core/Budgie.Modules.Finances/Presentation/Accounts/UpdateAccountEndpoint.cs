using Budgie.Modules.Finances.Application.Accounts.UpdateAccount;
using Budgie.Shared.Enums;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Finances.Presentation.Accounts;

internal static class UpdateAccountEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("accounts/{id:guid}", async (Guid id, [FromBody] UpdateAccountRequest request, ISender sender) =>
        {
            var command = new UpdateAccountCommand(
                id,
                request.Name,
                request.CreditLimit,
                request.CreditConsumptionMode,
                request.BillingCycleDay,
                request.PaymentDueDay,
                request.AnnualInterestRate,
                request.BillingCycleDayInclusive);

            var result = await sender.Send(command);

            return result.Match(
                _ => Results.NoContent(),
                errors => Results.BadRequest(errors));
        })
        .Produces(204)
        .WithName("UpdateAccount")
        .WithTags("Accounts");
    }
}

public record UpdateAccountRequest(
    string Name,
    decimal? CreditLimit,
    CreditConsumptionMode? CreditConsumptionMode,
    int? BillingCycleDay = null,
    int? PaymentDueDay = null,
    decimal? AnnualInterestRate = null,
    bool BillingCycleDayInclusive = true);
