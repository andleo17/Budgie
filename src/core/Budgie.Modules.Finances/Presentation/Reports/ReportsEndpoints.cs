using Budgie.Modules.Finances.Application.Reports;
using Budgie.Modules.Finances.Application.Reports.GetCategorySpending;
using Budgie.Modules.Finances.Application.Reports.GetDashboardSummary;
using Budgie.Modules.Finances.Application.Reports.GetGoalsStatus;
using Budgie.Shared.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Finances.Presentation.Reports;

public static class ReportsEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("reports").WithTags("Reports");

        group.MapGet("/dashboard", async (string? currency, IMediator mediator, IUserSettingsProvider settingsProvider) =>
        {
            var mainCurrency = currency ?? await settingsProvider.GetMainCurrencyAsync();
            var result = await mediator.Send(new GetDashboardSummaryQuery(mainCurrency));
            return result.Match(TypedResults.Ok, errors => Results.BadRequest(errors));
        })
        .Produces<DashboardSummary>(200);

        group.MapGet("/spending-by-category", async (DateTime? start, DateTime? end, IMediator mediator) =>
        {
            var now = DateTime.UtcNow;
            var startDate = start ?? new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
            var endDate = end ?? now;
            
            var result = await mediator.Send(new GetCategorySpendingQuery(startDate, endDate));
            return result.Match(TypedResults.Ok, errors => Results.BadRequest(errors));
        })
        .Produces<List<CategorySpending>>(200);

        group.MapGet("/goals", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetGoalsStatusQuery());
            return result.Match(TypedResults.Ok, errors => Results.BadRequest(errors));
        })
        .Produces<List<GoalStatus>>(200);
    }
}
