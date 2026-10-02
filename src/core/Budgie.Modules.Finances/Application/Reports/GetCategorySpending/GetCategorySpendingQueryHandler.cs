using Budgie.Modules.Finances.Application.Categories;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Reports.GetCategorySpending;

internal sealed class GetCategorySpendingQueryHandler(
    IReportRepository reportRepository,
    ICurrentUserProvider currentUserProvider,
    IUserSettingsProvider userSettingsProvider)
    : IRequestHandler<GetCategorySpendingQuery, ErrorOr<List<CategorySpending>>>
{
    public async ValueTask<ErrorOr<List<CategorySpending>>> Handle(GetCategorySpendingQuery request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var startDate = Guard.NormalizeDate(request.StartDate);
        var endDate = Guard.NormalizeDate(request.EndDate).Date.AddDays(1);

        var spending = await reportRepository.GetCategorySpendingAsync(userId, startDate, endDate, ct);
        if (!spending.Any(s => SystemCategoryNames.IsSystemCategory(s.CategoryId)))
            return spending;

        // Traduce nombres de categorías de sistema según el idioma del usuario.
        var language = await userSettingsProvider.GetLanguageAsync();
        return spending.Select(s => s with
        {
            CategoryName = SystemCategoryNames.Resolve(s.CategoryId, s.CategoryName, language) ?? s.CategoryName
        }).ToList();
    }
}
