using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Modules.Finances.Infrastructure.Logging;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.Categories.UpdateCategory;

internal sealed class UpdateCategoryCommandHandler(
    FinancesDbContext dbContext,
    ILogger<UpdateCategoryCommandHandler> logger)
    : IRequestHandler<UpdateCategoryCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (category is null) return Error.NotFound("Category.NotFound", "Category not found.");
        if (category.IsSystem) return Error.Forbidden("Category.IsSystem", "System categories cannot be modified.");

        if (request.ParentId.HasValue)
        {
            if (request.ParentId.Value == request.Id)
                return Error.Validation("Category.CircularReference", "A category cannot be its own parent.");

            var allCategories = await dbContext.Categories.ToListAsync(cancellationToken);
            var catMap = allCategories.ToDictionary(c => c.Id, c => (Category?)c);

            if (!catMap.ContainsKey(request.ParentId.Value))
                return Error.NotFound("Category.NotFound", "Parent category not found.");

            if (category.HasCircularReference(request.ParentId.Value, catMap))
                return Error.Validation("Category.CircularReference", "Circular reference detected.");

            var parentDepth = catMap[request.ParentId.Value]!.GetDepth(catMap);
            var currentDepth = category.GetDepth(catMap);
            var delta = (parentDepth + 1) - currentDepth;

            var deepestDescendant = category.GetSubtreeMaxDepth(catMap);
            if (deepestDescendant + delta > Category.MaxDepth - 1)
                return Error.Validation("Category.MaxDepthExceeded", "Moving this category would exceed the maximum depth of 3.");
        }

        category.Update(request.Name, request.Color, request.Icon, request.Type, request.ParentId);
        await dbContext.SaveChangesAsync(cancellationToken);

        LogMessages.CategoryUpdated(logger, request.Id);

        return Result.Success;
    }
}
