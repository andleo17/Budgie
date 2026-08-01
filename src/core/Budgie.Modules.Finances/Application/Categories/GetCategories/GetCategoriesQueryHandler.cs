using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.Categories.GetCategories;

internal sealed class GetCategoriesQueryHandler(
    FinancesDbContext dbContext,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetCategoriesQuery, ErrorOr<List<CategoryResponse>>>
{
    public async Task<ErrorOr<List<CategoryResponse>>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
    {
        var query = request.IncludeSystem
            ? dbContext.Categories.IgnoreQueryFilters()
                .Where(c => c.UserId == null || c.UserId == currentUserProvider.UserId)
            : dbContext.Categories.Where(c => !c.IsSystem);

        var categories = await query
            .Select(c => new CategoryResponse(c.Id, c.Name, c.Color, c.Icon, c.Type, c.ParentId, c.IsSystem))
            .ToListAsync(cancellationToken);

        return categories;
    }
}
