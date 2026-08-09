using Budgie.Modules.Relationships.Application.Tags;
using Budgie.Modules.Relationships.Application.Tags.DeleteTag;
using Budgie.Modules.Relationships.Application.Tags.GetTags;
using Budgie.Modules.Relationships.Application.Tags.UpdateTag;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Relationships.Presentation.Tags;

internal static class TagsEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("tags", async (ISender sender) =>
        {
            var query = new GetTagsQuery();
            var result = await sender.Send(query);
            return result.Match(tags => Results.Ok(tags), errors => Results.BadRequest(errors));
        })
        .Produces<List<TagResponse>>(200)
        .WithName("GetTags")
        .WithTags("Tags");

        app.MapPut("tags/{id:guid}", async (Guid id, [FromBody] UpdateTagRequest request, ISender sender) =>
        {
            var command = new UpdateTagCommand(id, request.Name, request.Color);
            var result = await sender.Send(command);
            return result.Match(_ => Results.NoContent(), errors => Results.BadRequest(errors));
        })
        .Produces(204)
        .WithName("UpdateTag")
        .WithTags("Tags");

        app.MapDelete("tags/{id:guid}", async (Guid id, ISender sender) =>
        {
            var command = new DeleteTagCommand(id);
            var result = await sender.Send(command);
            return result.Match(_ => Results.NoContent(), errors => Results.BadRequest(errors));
        })
        .Produces(204)
        .WithName("DeleteTag")
        .WithTags("Tags");
    }
}

internal record UpdateTagRequest(string Name, string? Color = null);
