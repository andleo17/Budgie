using Budgie.Shared.Abstractions;
using Budgie.Shared.Infrastructure;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Categories.GetCategories;

public record GetCategoriesQuery(bool IncludeSystem = false) : IRequest<ErrorOr<List<CategoryResponse>>>;

public record CategoryResponse(Guid Id, string Name, string? Color, string? Icon,
    int Type, bool IsSystem, Guid? ParentId);

internal sealed class GetCategoriesQueryHandler(
    IDbConnectionFactory factory,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetCategoriesQuery, ErrorOr<List<CategoryResponse>>>
{
    public async ValueTask<ErrorOr<List<CategoryResponse>>> Handle(GetCategoriesQuery request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();

        return request.IncludeSystem
            ? (await factory.QueryListAsync<CategoryResponse>(
                @"SELECT ""Id"",""Name"",""Color"",""Icon"",""Type"",""IsSystem"",""ParentId""
                  FROM ""Categories"" WHERE (""UserId"" IS NULL OR ""UserId"" = @UserId) ORDER BY ""Name""",
                new { UserId = userId }, ct)).ToList()
            : (await factory.QueryListAsync<CategoryResponse>(
                @"SELECT ""Id"",""Name"",""Color"",""Icon"",""Type"",""IsSystem"",""ParentId""
                  FROM ""Categories"" WHERE NOT ""IsSystem"" AND ""UserId"" = @UserId ORDER BY ""Name""",
                new { UserId = userId }, ct)).ToList();
    }
}
