using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Reports.GetCategorySpending;

internal sealed class GetCategorySpendingQueryHandler(
    IReportRepository reportRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetCategorySpendingQuery, ErrorOr<List<CategorySpending>>>
{
    public async ValueTask<ErrorOr<List<CategorySpending>>> Handle(GetCategorySpendingQuery request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var startDate = Guard.NormalizeDate(request.StartDate);
        var endDate = Guard.NormalizeDate(request.EndDate).Date.AddDays(1);

        var spending = await reportRepository.GetCategorySpendingAsync(userId, startDate, endDate, ct);
        return spending;
    }
}
