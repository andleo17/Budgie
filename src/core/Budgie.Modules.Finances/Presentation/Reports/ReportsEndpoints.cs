using Budgie.Modules.Finances.Application.Reports.GetCategoryBudgets;
using Budgie.Modules.Finances.Application.Reports.GetCategorySpending;
using Budgie.Modules.Finances.Application.Reports.GetDashboardSummary;
using Budgie.Modules.Finances.Application.Reports.GetTransactionsSummary;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Finances.Presentation.Reports;

internal static class ReportsEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/reports/dashboard", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetDashboardSummaryQuery());
            return result.Match(Results.Ok, err => Results.BadRequest(err));
        })
        .Produces<DashboardSummary>(StatusCodes.Status200OK);

        group.MapGet("/reports/budgets", async (DateTime? month, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetCategoryBudgetsQuery(month));
            return result.Match(Results.Ok, err => Results.BadRequest(err));
        })
        .Produces<List<CategoryBudget>>(StatusCodes.Status200OK);

        group.MapGet("/transactions/summary", async (DateTime? startDate, DateTime? endDate,
            string? accountIds, IMediator mediator) =>
        {
            var accountIdList = accountIds?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Guid.Parse).ToList();
            var result = await mediator.Send(new GetTransactionsSummaryQuery(startDate, endDate,
                accountIdList as IReadOnlyList<Guid>));
            return result.Match(Results.Ok, err => Results.BadRequest(err));
        })
        .Produces<GetTransactionsSummaryResponse>(StatusCodes.Status200OK);

        group.MapGet("/reports/spending-by-category", async (DateTime? start, DateTime? end,
            IMediator mediator) =>
        {
            var now = DateTime.UtcNow;
            var startDate = start ?? new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
            var endDate = end ?? now;

            var result = await mediator.Send(new GetCategorySpendingQuery(startDate, endDate));
            return result.Match(Results.Ok, err => Results.BadRequest(err));
        })
        .Produces<List<CategorySpending>>(StatusCodes.Status200OK);
    }
}
