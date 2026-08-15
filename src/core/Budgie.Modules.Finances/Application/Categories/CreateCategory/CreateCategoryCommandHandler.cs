using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Categories.CreateCategory;

public record CreateCategoryCommand(
    string Name, int Type, string? Color = null, string? Icon = null, Guid? ParentId = null, decimal? MonthlyBudget = null)
    : IRequest<ErrorOr<Guid>>;

internal sealed class CreateCategoryCommandHandler(
    ICategoryRepository categoryRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<CreateCategoryCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(CreateCategoryCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();

        if (request.ParentId.HasValue)
        {
            var all = await categoryRepository.GetByUserIdAsync(userId, ct);
            var catMap = all.ToDictionary(c => c.Id, c => (Category?)c);

            if (!catMap.TryGetValue(request.ParentId.Value, out var parent) || parent is null)
                return Error.NotFound("Category.NotFound", "Parent category not found.");

            var parentDepth = parent.GetDepth(catMap);
            if (parentDepth >= Category.MaxDepth - 1)
                return Error.Validation("Category.MaxDepthExceeded", "Maximum category depth is 3.");
        }

        var category = Category.Create(userId, request.Name, request.Type, request.Color, request.Icon, request.ParentId, request.MonthlyBudget);
        await categoryRepository.AddAsync(category, ct);
        return category.Id;
    }
}
