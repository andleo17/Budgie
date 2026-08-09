using Budgie.Modules.Finances.Application.Goals.UpdateGoal;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Finances.Presentation.Goals;

internal static class UpdateGoalEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("goals/{id:guid}", async (Guid id, [FromBody] UpdateGoalRequest request, ISender sender) =>
        {
            var command = new UpdateGoalCommand(id, request.TargetAmount, request.TargetDate);

            var result = await sender.Send(command);

            return result.Match(
                _ => Results.NoContent(),
                errors => Results.BadRequest(errors));
        })
        .Produces(204)
        .WithName("UpdateGoal")
        .WithTags("Goals");
    }
}

public record UpdateGoalRequest(decimal TargetAmount, DateTime? TargetDate);
