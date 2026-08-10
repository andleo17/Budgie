using Budgie.Shared.Infrastructure;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Categories.GetCategories;

public record GetCategoriesQuery : IRequest<ErrorOr<List<CategoryResponse>>>;

public record CategoryResponse(Guid Id, string Name, string? Color, string? Icon,
    int Type, bool IsSystem, Guid? ParentId);

internal sealed class GetCategoriesQueryHandler(IDbConnectionFactory factory)
    : IRequestHandler<GetCategoriesQuery, ErrorOr<List<CategoryResponse>>>
{
    public async ValueTask<ErrorOr<List<CategoryResponse>>> Handle(GetCategoriesQuery request, CancellationToken ct)
    {
        return (await factory.QueryListAsync<CategoryResponse>(
            @"SELECT ""Id"",""Name"",""Color"",""Icon"",""Type"",""IsSystem"",""ParentId"" FROM ""Categories"" ORDER BY ""Name""", null, ct)).ToList();
    }
}
