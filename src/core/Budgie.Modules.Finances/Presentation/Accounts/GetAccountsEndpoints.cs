using Budgie.Modules.Finances.Application.Accounts;
using Budgie.Modules.Finances.Application.Accounts.GetAccounts;
using Budgie.Modules.Finances.Application.Accounts.GetAccountById;
using ErrorOr;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Finances.Presentation.Accounts;

internal static class GetAccountsEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("accounts", async (ISender sender) =>
        {
            var query = new GetAccountsQuery();
            var result = await sender.Send(query);

            return result.Match(
                accounts => Results.Ok(accounts),
                errors => Results.BadRequest(errors));
        })
        .Produces<List<AccountResponse>>(200)
        .WithName("GetAccounts")
        .WithTags("Accounts");

        app.MapGet("accounts/{id:guid}", async (Guid id, ISender sender) =>
        {
            var query = new GetAccountByIdQuery(id);
            var result = await sender.Send(query);

            return result.Match(
                account => Results.Ok(account),
                errors => errors[0].Type == ErrorType.NotFound
                    ? Results.NotFound()
                    : Results.BadRequest(errors));
        })
        .Produces<AccountResponse>(200)
        .WithName("GetAccountById")
        .WithTags("Accounts");
    }
}
