using Budgie.Modules.Finances.Application.Accounts;
using Budgie.Modules.Finances.Application.Accounts.GetAccountById;
using Budgie.Modules.Finances.Application.Accounts.GetAccounts;
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
        });
        group.MapGet("/accounts/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetAccountByIdQuery(id));
            return result.Match(Results.Ok, err => Results.BadRequest(err));
        });
    }
}
