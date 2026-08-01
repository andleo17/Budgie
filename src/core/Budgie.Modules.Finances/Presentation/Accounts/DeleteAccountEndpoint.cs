using Budgie.Modules.Finances.Application.Accounts.DeleteAccount;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Finances.Presentation.Accounts;

internal static class DeleteAccountEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("accounts/{id:guid}", async (Guid id, ISender sender) =>
        {
            var command = new DeleteAccountCommand(id);
            var result = await sender.Send(command);

            return result.Match(
                _ => Results.NoContent(),
                errors => Results.BadRequest(errors));
        })
        .WithName("DeleteAccount")
        .WithTags("Accounts");
    }
}
