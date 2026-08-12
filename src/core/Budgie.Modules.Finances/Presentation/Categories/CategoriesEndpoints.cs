using Budgie.Modules.Finances.Application.Categories.CreateCategory;
using Budgie.Modules.Finances.Application.Categories.DeleteCategory;
using Budgie.Modules.Finances.Application.Categories.GetCategories;
using Budgie.Modules.Finances.Application.Categories.UpdateCategory;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Finances.Presentation.Categories;

internal static class CategoriesEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/categories", async (bool includeSystem = false, IMediator mediator = null!) =>
        {
            var result = await mediator.Send(new GetCategoriesQuery(includeSystem));
            return result.Match(Results.Ok, err => Results.BadRequest(err));
        });
        group.MapPost("/categories", async (CreateCategoryCommand cmd, IMediator mediator) =>
        {
            var result = await mediator.Send(cmd);
            return result.Match(
                id => Results.Created($"/api/finances/categories/{id}", new { id }),
                err => Results.BadRequest(err.ToDictionary(e => e.Code, e => new[] { e.Description })));
        });
        group.MapPut("/categories/{id:guid}", async (Guid id, UpdateCategoryRequest req, IMediator mediator) =>
        {
            var cmd = new UpdateCategoryCommand(id, req.Name, req.Type ?? 1, req.Color, req.Icon, req.ParentId);
            var result = await mediator.Send(cmd);
            return result.Match(_ => Results.NoContent(), err => Results.BadRequest(err));
        });
        group.MapDelete("/categories/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new DeleteCategoryCommand(id));
            return result.Match(_ => Results.NoContent(), err => Results.BadRequest(err));
        });
    }
}

public record UpdateCategoryRequest(
    string Name, string? Color = null, string? Icon = null, int? Type = null, Guid? ParentId = null);