using Budgie.Shared.Abstractions;
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
        var mainCurrency = await GetMainCurrencyAsync(factory, userId, ct);

        var summary = await factory.QuerySingleAsync<DashboardSummary>(@"
            WITH account_totals AS (
                SELECT COALESCE(SUM(CASE WHEN ""Currency"" = @Currency THEN ""Balance"" ELSE 0 END), 0) AS totalBalance
                FROM ""Accounts"" WHERE ""UserId"" = @UserId
            ),
            monthly_flow AS (
                SELECT
                    COALESCE(SUM(CASE WHEN t.""Type"" = 0 AND t.""Currency"" = @Currency THEN t.""Amount"" ELSE 0 END), 0) AS monthlyIncome,
                    COALESCE(SUM(CASE WHEN t.""Type"" = 1 AND t.""Currency"" = @Currency THEN t.""Amount"" ELSE 0 END), 0) AS monthlyExpenses
                FROM ""Transactions"" t WHERE t.""UserId"" = @UserId
                  AND t.""Date"" >= DATE_TRUNC('month', NOW())
                  AND t.""ParentTransactionId"" IS NULL
            )
            SELECT at.totalBalance, mf.monthlyIncome, mf.monthlyExpenses,
                   (mf.monthlyIncome - mf.monthlyExpenses) AS netFlow,
                   @Currency AS currency
            FROM account_totals at, monthly_flow mf",
            new { UserId = userId, Currency = mainCurrency }, ct);

        return summary with { Currency = mainCurrency };
    }

    private static async Task<string> GetMainCurrencyAsync(IDbConnectionFactory factory, Guid userId, CancellationToken ct)
    {
        return await factory.QuerySingleOrDefaultAsync<string>(
            @"SELECT ""MainCurrency"" FROM ""UserSettings"" WHERE ""UserId"" = @UserId",
            new { UserId = userId }, ct) ?? "USD";
    }
}
