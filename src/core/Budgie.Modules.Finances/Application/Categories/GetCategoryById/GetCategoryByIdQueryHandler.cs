using Budgie.Modules.Finances.Infrastructure.Database;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.Categories.GetCategoryById;

internal sealed class GetCategoryByIdQueryHandler(FinancesDbContext dbContext)
    : IRequestHandler<GetCategoryByIdQuery, ErrorOr<CategoryResponse>>
{
    public async Task<ErrorOr<CategoryResponse>> Handle(GetCategoryByIdQuery request, CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories
            .Where(c => c.Id == request.Id)
            .Select(c => new CategoryResponse(c.Id, c.Name, c.Color, c.Icon, c.Type, c.ParentId))
            .FirstOrDefaultAsync(cancellationToken);

        if (category is null)
            return Error.NotFound("Category.NotFound", "Category not found.");

        return category;
    }
}
