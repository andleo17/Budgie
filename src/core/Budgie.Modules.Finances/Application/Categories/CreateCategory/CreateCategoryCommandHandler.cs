using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Modules.Finances.Infrastructure.Logging;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.Categories.CreateCategory;

internal sealed class CreateCategoryCommandHandler(
    FinancesDbContext dbContext,
    ICurrentUserProvider currentUserProvider,
    ILogger<CreateCategoryCommandHandler> logger) 
    : IRequestHandler<CreateCategoryCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        if (request.ParentId.HasValue)
        {
            var allCategories = await dbContext.Categories.ToListAsync(cancellationToken);
            var catMap = allCategories.ToDictionary(c => c.Id, c => (Category?)c);

            if (!catMap.TryGetValue(request.ParentId.Value, out var parent) || parent is null)
                return Error.NotFound("Category.NotFound", "Parent category not found.");

            var parentDepth = parent.GetDepth(catMap);
            if (parentDepth >= Category.MaxDepth - 1)
                return Error.Validation("Category.MaxDepthExceeded", "Maximum category depth is 3.");
        }

        var category = Category.Create(currentUserProvider.GetUserId(), request.Name, request.Type, request.Color, request.Icon, request.ParentId);

        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        LogMessages.CategoryCreated(logger, category.Id, category.Name);

        return category.Id;
    }
}
