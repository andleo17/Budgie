using Budgie.Modules.Finances.Application.Categories;
using Budgie.Modules.Finances.Application.Categories.GetCategories;
using Budgie.Modules.Finances.Application.Categories.GetCategoryById;
using Budgie.Modules.Finances.Application.Categories.DeleteCategory;
using Budgie.Modules.Finances.Application.Categories.UpdateCategory;
using ErrorOr;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Finances.Presentation.Categories;

internal static class CategoriesEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("categories", async ([FromQuery] bool includeSystem = false, ISender sender = default!) =>
        {
            var query = new GetCategoriesQuery(includeSystem);
            var result = await sender.Send(query);
            return result.Match(categories => Results.Ok(categories), errors => Results.BadRequest(errors));
        })
        .Produces<List<CategoryResponse>>(200)
        .WithName("GetCategories")
        .WithTags("Categories");

        app.MapGet("categories/{id:guid}", async (Guid id, ISender sender) =>
        {
            var query = new GetCategoryByIdQuery(id);
            var result = await sender.Send(query);
            return result.Match(
                category => Results.Ok(category),
                errors => errors[0].Type == ErrorType.NotFound
                    ? Results.NotFound()
                    : Results.BadRequest(errors));
        })
        .Produces<CategoryResponse>(200)
        .WithName("GetCategoryById")
        .WithTags("Categories");

        app.MapPut("categories/{id:guid}", async (Guid id, [FromBody] UpdateCategoryRequest request, ISender sender) =>
        {
            var command = new UpdateCategoryCommand(id, request.Name, request.Color, request.Icon, request.Type, request.ParentId);
            var result = await sender.Send(command);
            return result.Match(_ => Results.NoContent(), errors => Results.BadRequest(errors));
        })
        .Produces(204)
        .WithName("UpdateCategory")
        .WithTags("Categories");

        app.MapDelete("categories/{id:guid}", async (Guid id, ISender sender) =>
        {
            var command = new DeleteCategoryCommand(id);
            var result = await sender.Send(command);
            return result.Match(_ => Results.NoContent(), errors => Results.BadRequest(errors));
        })
        .Produces(204)
        .WithName("DeleteCategory")
        .WithTags("Categories");
    }
}

public record UpdateCategoryRequest(string Name, string? Color, string? Icon, int? Type = null, Guid? ParentId = null);
