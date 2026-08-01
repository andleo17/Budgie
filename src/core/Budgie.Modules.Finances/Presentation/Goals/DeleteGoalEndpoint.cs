using Budgie.Modules.Finances.Application.Goals.DeleteGoal;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Finances.Presentation.Goals;

internal static class DeleteGoalEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("goals/{id:guid}", async (Guid id, ISender sender) =>
        {
            var command = new DeleteGoalCommand(id);
            var result = await sender.Send(command);

            return result.Match(
                _ => Results.NoContent(),
                errors => Results.BadRequest(errors));
        })
        .WithName("DeleteGoal")
        .WithTags("Goals");
    }
}
