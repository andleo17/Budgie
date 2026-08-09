using Budgie.Modules.Finances.Application.Goals.CreateGoal;
using Budgie.Shared.Dtos;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Finances.Presentation.Goals;

public static class CreateGoalEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("goals", async (CreateGoalCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);

            return result.Match<IResult>(
                goalId => TypedResults.Created($"/api/finances/goals/{goalId}", new { Id = goalId }),
                errors => TypedResults.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description }))
            );
        })
        .Produces<CreatedResponse>(StatusCodes.Status201Created)
        .WithTags("Goals");
    }
}
