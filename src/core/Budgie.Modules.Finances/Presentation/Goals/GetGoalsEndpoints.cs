using Budgie.Modules.Finances.Application.Goals;
using Budgie.Modules.Finances.Application.Goals.GetGoals;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Finances.Presentation.Goals;

internal static class GetGoalsEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("goals", async (ISender sender) =>
        {
            var query = new GetGoalsQuery();
            var result = await sender.Send(query);

            return result.Match(
                goals => Results.Ok(goals),
                errors => Results.BadRequest(errors));
        })
        .Produces<List<GoalResponse>>(200)
        .WithName("GetGoals")
        .WithTags("Goals");
    }
}
