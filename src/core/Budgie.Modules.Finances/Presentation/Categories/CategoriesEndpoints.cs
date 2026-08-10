using Budgie.Modules.Finances.Application.Categories.GetCategories;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Finances.Presentation.Categories;

internal static class CategoriesEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/categories", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetCategoriesQuery());
            return result.Match(Results.Ok, err => Results.BadRequest(err));
        });
    }
}
