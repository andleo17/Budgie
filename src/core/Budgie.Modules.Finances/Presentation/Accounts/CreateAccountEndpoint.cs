using Budgie.Modules.Finances.Application.Accounts.CreateAccount;
using Budgie.Shared.Dtos;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Finances.Presentation.Accounts;

public static class CreateAccountEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("accounts", async (CreateAccountCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);

            return result.Match<IResult>(
                accountId => TypedResults.Created($"/api/finances/accounts/{accountId}", new { Id = accountId }),
                errors => TypedResults.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description }))
            );
        })
        .Produces<CreatedResponse>(StatusCodes.Status201Created)
        .WithTags("Accounts");
    }
}
