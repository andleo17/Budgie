using Budgie.Modules.Finances.Application;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Categories.GetCategories;

public record GetCategoriesQuery(bool IncludeSystem = false) : IRequest<ErrorOr<List<CategoryResponse>>>;

internal sealed class GetCategoriesQueryHandler(
    ICategoryRepository categoryRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetCategoriesQuery, ErrorOr<List<CategoryResponse>>>
{
    public async ValueTask<ErrorOr<List<CategoryResponse>>> Handle(GetCategoriesQuery request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var categories = await categoryRepository.GetResponsesByUserIdAsync(userId, request.IncludeSystem, ct);
        return categories;
    }
}
