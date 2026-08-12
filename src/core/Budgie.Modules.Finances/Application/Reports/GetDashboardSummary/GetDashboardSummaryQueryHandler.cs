using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using Budgie.Shared.Infrastructure;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Reports.GetDashboardSummary;

public record GetDashboardSummaryQuery : IRequest<ErrorOr<DashboardSummary>>;

public record DashboardSummary(decimal TotalBalance, decimal MonthlyIncome,
    decimal MonthlyExpenses, decimal NetFlow, string Currency);

internal sealed class GetDashboardSummaryQueryHandler(
    IDbConnectionFactory factory,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetDashboardSummaryQuery, ErrorOr<DashboardSummary>>
{
    public async ValueTask<ErrorOr<DashboardSummary>> Handle(GetDashboardSummaryQuery request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();

        var summary = await factory.QuerySingleAsync<DashboardSummary>(@"
            WITH account_totals AS (
                SELECT COALESCE(SUM(""Balance""), 0) AS totalBalance
                FROM ""Accounts"" WHERE ""UserId"" = @UserId
            ),
            monthly_flow AS (
                SELECT
                    COALESCE(SUM(CASE WHEN t.""Type"" IN (0,4) THEN t.""Amount"" ELSE 0 END), 0) AS monthlyIncome,
                    COALESCE(SUM(CASE WHEN t.""Type"" IN (1,3) THEN t.""Amount"" ELSE 0 END), 0) AS monthlyExpenses
                FROM ""Transactions"" t WHERE t.""UserId"" = @UserId
                  AND t.""Date"" >= DATE_TRUNC('month', NOW())
                  AND t.""ParentTransactionId"" IS NULL
            )
            SELECT at.totalBalance, mf.monthlyIncome, mf.monthlyExpenses,
                   (mf.monthlyIncome - mf.monthlyExpenses) AS netFlow,
                   @Currency AS currency
            FROM account_totals at, monthly_flow mf",
            new { UserId = userId, Currency = await GetMainCurrencyAsync(factory, userId, ct) }, ct);

        return summary;
    }

    private static async Task<string> GetMainCurrencyAsync(IDbConnectionFactory factory, Guid userId, CancellationToken ct)
    {
        return await factory.QuerySingleOrDefaultAsync<string>(
            @"SELECT ""MainCurrency"" FROM ""UserSettings"" WHERE ""UserId"" = @UserId",
            new { UserId = userId }, ct) ?? "USD";
    }
}
