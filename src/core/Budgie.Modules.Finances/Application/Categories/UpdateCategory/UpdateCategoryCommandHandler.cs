using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Categories.UpdateCategory;

public record UpdateCategoryCommand(
    Guid Id, string Name, int Type, string? Color = null, string? Icon = null, Guid? ParentId = null)
    : IRequest<ErrorOr<Success>>;

internal sealed class UpdateCategoryCommandHandler(
    ICategoryRepository categoryRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<UpdateCategoryCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(UpdateCategoryCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var category = await categoryRepository.GetByIdAsync(request.Id, userId, ct);
        if (category is null) return Error.NotFound("Category.NotFound", "Category not found.");
        if (category.IsSystem) return Error.Forbidden("Category.IsSystem", "System categories cannot be modified.");

        if (request.ParentId.HasValue)
        {
            if (request.ParentId.Value == request.Id)
                return Error.Validation("Category.CircularReference", "A category cannot be its own parent.");

            var all = await categoryRepository.GetByUserIdAsync(userId, ct);
            var catMap = all.ToDictionary(c => c.Id, c => (Category?)c);

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
        await categoryRepository.UpdateAsync(category, ct);
        return Result.Success;
    }
}
