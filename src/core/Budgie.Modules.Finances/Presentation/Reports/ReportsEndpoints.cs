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
        });

        group.MapGet("/transactions/summary", async (DateTime? startDate, DateTime? endDate,
            string? accountIds, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetTransactionsSummaryQuery(startDate, endDate, accountIds));
            return result.Match(Results.Ok, err => Results.BadRequest(err));
        });
    }
}
