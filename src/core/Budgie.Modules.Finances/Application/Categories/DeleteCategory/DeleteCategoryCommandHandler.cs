using Budgie.Modules.Finances.Application;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Categories.DeleteCategory;

public record DeleteCategoryCommand(Guid Id) : IRequest<ErrorOr<Success>>;

internal sealed class DeleteCategoryCommandHandler(
    ICategoryRepository categoryRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<DeleteCategoryCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(DeleteCategoryCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var category = await categoryRepository.GetByIdAsync(request.Id, userId, ct);
        if (category is null) return Error.NotFound("Category.NotFound", "Category not found.");
        if (category.IsSystem) return Error.Forbidden("Category.IsSystem", "System categories cannot be deleted.");

        await categoryRepository.DeleteAsync(request.Id, userId, ct);
        return Result.Success;
    }
}
