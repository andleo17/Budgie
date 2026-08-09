using Budgie.Modules.Relationships.Application.Tags.CreateTag;
using Budgie.Shared.Dtos;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Relationships.Presentation.Tags;

public static class CreateTagEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("tags", async (CreateTagCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);

            return result.Match<IResult>(
                tagId => TypedResults.Created($"/api/relationships/tags/{tagId}", new { Id = tagId }),
                errors => TypedResults.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description }))
            );
        })
        .Produces<CreatedResponse>(StatusCodes.Status201Created)
        .WithTags("Tags");
    }
}
