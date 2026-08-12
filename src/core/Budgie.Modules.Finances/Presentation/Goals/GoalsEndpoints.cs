using Budgie.Modules.Finances.Application.Goals.CreateGoal;
using Budgie.Modules.Finances.Application.Goals.DeleteGoal;
using Budgie.Modules.Finances.Application.Goals.GetGoals;
using Budgie.Modules.Finances.Application.Goals.UpdateGoal;
using Budgie.Modules.Finances.Application.Reports.GetGoalsStatus;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Finances.Presentation.Goals;

internal static class GoalsEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/goals", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetGoalsQuery());
            return result.Match(Results.Ok, err => Results.BadRequest(err));
        });

        group.MapPost("/goals", async (CreateGoalCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return result.Match(
                id => Results.Created($"/api/finances/goals/{id}", new { id }),
                errors => Results.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
        });

        group.MapPut("/goals/{id:guid}", async (Guid id, UpdateGoalRequest request, IMediator mediator) =>
        {
            var command = new UpdateGoalCommand(id, request.TargetAmount, request.TargetDate);
            var result = await mediator.Send(command);
            return result.Match(_ => Results.NoContent(), err => Results.BadRequest(err));
        });

        group.MapDelete("/goals/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new DeleteGoalCommand(id));
            return result.Match(_ => Results.NoContent(), err => Results.BadRequest(err));
        });

        group.MapGet("/reports/goals", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetGoalsStatusQuery());
            return result.Match(Results.Ok, err => Results.BadRequest(err));
        });
    }
}

public record UpdateGoalRequest(decimal TargetAmount, DateTime? TargetDate);
