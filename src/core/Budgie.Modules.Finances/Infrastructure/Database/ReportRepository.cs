using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Application.Reports.GetCategoryBudgets;
using Budgie.Modules.Finances.Application.Reports.GetCategorySpending;
using Budgie.Modules.Finances.Application.Reports.GetDashboardSummary;
using Budgie.Modules.Finances.Application.Reports.GetGoalsStatus;
using Budgie.Modules.Finances.Application.Reports.GetTransactionsSummary;
using Budgie.Shared.Infrastructure;

namespace Budgie.Modules.Finances.Infrastructure.Database;

internal sealed class ReportRepository(IDbConnectionFactory factory) : IReportRepository
{
    public async Task<DashboardSummary> GetDashboardSummaryAsync(Guid userId, string mainCurrency, CancellationToken ct)
    {
        return await factory.QuerySingleAsync<DashboardSummary>(@"
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
            new { UserId = userId, Currency = mainCurrency }, ct);
    }

    public async Task<List<CategorySpending>> GetCategorySpendingAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken ct)
    {
        var rows = (await factory.QueryListAsync<SpendingRow>(
            @"SELECT
                t.""CategoryId"" AS CategoryId,
                COALESCE(c.""Name"", 'Uncategorized') AS CategoryName,
                c.""Color"" AS Color,
                COALESCE(SUM(t.""Amount"" - COALESCE(p.Contribution, 0)), 0) AS TotalAmount
              FROM ""Transactions"" t
              LEFT JOIN ""Categories"" c ON c.""Id"" = t.""CategoryId""
              LEFT JOIN (
                  SELECT ""TransactionId"", SUM(""ContributedAmount"") AS Contribution
                  FROM ""TransactionParticipants""
                  GROUP BY ""TransactionId""
              ) p ON p.""TransactionId"" = t.""Id""
              WHERE t.""UserId"" = @UserId
                AND t.""Type"" = 1
                AND t.""Date"" >= @StartDate AND t.""Date"" < @EndDate
              GROUP BY t.""CategoryId"", c.""Name"", c.""Color""",
            new { UserId = userId, StartDate = startDate, EndDate = endDate }, ct)).ToList();

        var spendingByCategory = rows
            .Select(r => new CategorySpending(
                r.CategoryId ?? Guid.Empty,
                r.CategoryName,
                r.Color,
                r.TotalAmount,
                0))
            .ToList();

        var grandTotal = spendingByCategory.Sum(s => s.TotalAmount);
        if (grandTotal > 0)
        {
            spendingByCategory = spendingByCategory
                .Select(s => s with { Percentage = (double)(s.TotalAmount / grandTotal * 100) })
                .OrderByDescending(s => s.TotalAmount)
                .ToList();
        }

        return spendingByCategory;
    }

    public async Task<List<CategoryBudget>> GetCategoryBudgetsAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken ct)
    {
        const string sql = @"
            WITH RECURSIVE cat_tree AS (
                SELECT c.""Id"", c.""Name"", c.""Color"", c.""Icon"", c.""MonthlyBudget"", c.""Id"" AS root_id
                FROM ""Categories"" c
                WHERE c.""UserId"" = @UserId AND c.""MonthlyBudget"" IS NOT NULL
                UNION ALL
                SELECT c.""Id"", c.""Name"", c.""Color"", c.""Icon"", c.""MonthlyBudget"", ct.root_id
                FROM ""Categories"" c
                JOIN cat_tree ct ON c.""ParentId"" = ct.""Id""
            ),
            spent AS (
                SELECT t.""CategoryId"", COALESCE(SUM(t.""Amount"" - COALESCE(p.Contribution, 0)), 0) AS Spent
                FROM ""Transactions"" t
                LEFT JOIN (
                    SELECT ""TransactionId"", SUM(""ContributedAmount"") AS Contribution
                    FROM ""TransactionParticipants""
                    GROUP BY ""TransactionId""
                ) p ON p.""TransactionId"" = t.""Id""
                WHERE t.""UserId"" = @UserId
                  AND t.""Type"" = 1
                  AND t.""Date"" >= @StartDate AND t.""Date"" < @EndDate
                GROUP BY t.""CategoryId""
            )
            SELECT ct.root_id AS CategoryId,
                MIN(ct.""Name"") AS CategoryName,
                MIN(ct.""Color"") AS Color,
                MIN(ct.""Icon"") AS Icon,
                MIN(ct.""MonthlyBudget"") AS MonthlyBudget,
                COALESCE(SUM(s.Spent), 0) AS Spent,
                CAST(0 AS NUMERIC) AS Remaining, CAST(0 AS DOUBLE PRECISION) AS Percentage, 0 AS Status
            FROM cat_tree ct
            LEFT JOIN spent s ON s.""CategoryId"" = ct.""Id""
            GROUP BY ct.root_id
            ORDER BY CategoryName";

        var rows = (await factory.QueryListAsync<CategoryBudgetRow>(sql,
            new { UserId = userId, StartDate = startDate, EndDate = endDate }, ct)).ToList();

        return rows.Select(r => new CategoryBudget(
            r.CategoryId, r.CategoryName, r.Color, r.Icon,
            r.MonthlyBudget, r.Spent, r.Remaining, r.Percentage,
            (BudgetThresholdStatus)r.Status)).ToList();
    }

    public async Task<List<GoalStatus>> GetGoalsStatusAsync(Guid userId, CancellationToken ct)
    {
        var rows = await factory.QueryListAsync<GoalStatusRow>(
            @"SELECT a.""Id"" AS AccountId, a.""Name"" AS AccountName, a.""Balance"" AS CurrentBalance,
                    g.""TargetAmount"", g.""TargetDate""
              FROM ""Goals"" g
              INNER JOIN ""Accounts"" a ON a.""Id"" = g.""AccountId""
              WHERE g.""UserId"" = @UserId AND g.""TargetAmount"" > 0",
            new { UserId = userId }, ct);

        return rows.Select(r => new GoalStatus(
            r.AccountId,
            r.AccountName,
            r.CurrentBalance,
            r.TargetAmount,
            (double)(r.CurrentBalance / r.TargetAmount * 100),
            r.TargetDate)).ToList();
    }

    public async Task<GetTransactionsSummaryResponse> GetTransactionsSummaryAsync(
        Guid userId, DateTime startDate, DateTime endDate, IReadOnlyList<Guid>? accountIds, string mainCurrency, CancellationToken ct)
    {
        var currencies = (await factory.QueryListAsync<CurrencySummary>(
            @"SELECT
                COALESCE(SUM(CASE WHEN t.""Type"" IN (0,4) THEN
                    CASE WHEN t.""Currency"" = a.""Currency"" THEN t.""Amount"" ELSE t.""Amount"" * t.""ExchangeRate"" END ELSE 0 END), 0) AS Income,
                COALESCE(SUM(CASE WHEN t.""Type"" IN (1,2,3) THEN
                    CASE WHEN t.""Currency"" = a.""Currency"" THEN t.""Amount"" ELSE t.""Amount"" * t.""ExchangeRate"" END ELSE 0 END), 0) AS Expenses,
                COALESCE(SUM(CASE WHEN t.""Type"" IN (0,4) THEN
                    CASE WHEN t.""Currency"" = a.""Currency"" THEN t.""Amount"" ELSE t.""Amount"" * t.""ExchangeRate"" END
                    ELSE -(CASE WHEN t.""Currency"" = a.""Currency"" THEN t.""Amount"" ELSE t.""Amount"" * t.""ExchangeRate"" END) END), 0) AS Balance,
                a.""Currency"" AS Currency
            FROM ""Transactions"" t
            JOIN ""Accounts"" a ON a.""Id"" = t.""AccountId""
            WHERE t.""UserId"" = @UserId
              AND t.""Date"" >= @StartDate AND t.""Date"" < @EndDate
              AND t.""ParentTransactionId"" IS NULL
              AND (@HasAccountFilter = FALSE OR t.""AccountId"" = ANY(@AccountIds))
            GROUP BY a.""Currency""",
            new
            {
                UserId = userId, StartDate = startDate, EndDate = endDate,
                HasAccountFilter = accountIds is { Count: > 0 },
                AccountIds = accountIds?.ToArray()
            }, ct)).ToList();

        currencies = currencies.OrderByDescending(c => Math.Abs(c.Balance)).ToList();

        var approximateIncome = 0m;
        var approximateExpenses = 0m;
        var accountRows = (await factory.QueryListAsync<AccountCurrencyRow>(
            @"SELECT ""Id"", ""Currency"" FROM ""Accounts"" WHERE ""UserId"" = @UserId",
            new { UserId = userId }, ct)).ToList();

        var monthlyTxs = (await factory.QueryListAsync<TxFlowRow>(
            @"SELECT ""Id"",""AccountId"",""Amount"",""Currency"",""ExchangeRate"",""Type"" FROM ""Transactions""
              WHERE ""UserId"" = @UserId AND ""Date"" >= @StartDate AND ""Date"" < @EndDate
                AND ""ParentTransactionId"" IS NULL
                AND (@HasAccountFilter = FALSE OR ""AccountId"" = ANY(@AccountIds))",
            new
            {
                UserId = userId, StartDate = startDate, EndDate = endDate,
                HasAccountFilter = accountIds is { Count: > 0 },
                AccountIds = accountIds?.ToArray()
            }, ct)).ToList();

        foreach (var t in monthlyTxs)
        {
            var isIncome = t.Type is 0 or 4;
            var isExpense = t.Type is 1 or 2 or 3;
            if (!isIncome && !isExpense) continue;

            var accountCurrency = accountRows.FirstOrDefault(a => a.Id == t.AccountId)?.Currency ?? mainCurrency;
            var inMain = accountCurrency == mainCurrency
                ? t.Amount * t.ExchangeRate
                : t.Currency == mainCurrency
                    ? t.Amount
                    : t.Amount * t.ExchangeRate;

            if (isIncome) approximateIncome += inMain;
            if (isExpense) approximateExpenses += inMain;
        }

        return new GetTransactionsSummaryResponse(currencies, mainCurrency, approximateIncome, approximateExpenses);
    }

    private record SpendingRow(Guid? CategoryId, string CategoryName, string? Color, decimal TotalAmount);
    private record GoalStatusRow(Guid AccountId, string AccountName, decimal CurrentBalance, decimal TargetAmount, DateTime? TargetDate);
    private record AccountCurrencyRow(Guid Id, string Currency);
    private record TxFlowRow(Guid Id, Guid AccountId, decimal Amount, string Currency, decimal ExchangeRate, int Type);
    private record CategoryBudgetRow(Guid CategoryId, string CategoryName, string? Color, string? Icon,
        decimal MonthlyBudget, decimal Spent, decimal Remaining, double Percentage, int Status);
}
