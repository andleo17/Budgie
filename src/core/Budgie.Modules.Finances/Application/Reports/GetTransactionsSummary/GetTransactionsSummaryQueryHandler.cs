using Budgie.Shared.Abstractions;
using Budgie.Shared.Infrastructure;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Reports.GetTransactionsSummary;

public record GetTransactionsSummaryQuery(DateTime? StartDate = null, DateTime? EndDate = null,
    IReadOnlyList<Guid>? AccountIds = null) : IRequest<ErrorOr<GetTransactionsSummaryResponse>>;

public record GetTransactionsSummaryResponse(
    List<CurrencySummary> Currencies, string MainCurrency,
    decimal ApproximateIncome, decimal ApproximateExpenses);

public record CurrencySummary(decimal Income, decimal Expenses, decimal Balance, string Currency);

internal sealed class GetTransactionsSummaryQueryHandler(
    IDbConnectionFactory factory,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetTransactionsSummaryQuery, ErrorOr<GetTransactionsSummaryResponse>>
{
    public async ValueTask<ErrorOr<GetTransactionsSummaryResponse>> Handle(GetTransactionsSummaryQuery request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var now = DateTime.UtcNow;
        var startDate = request.StartDate ?? now.AddDays(-30);
        var endDate = (request.EndDate ?? now).Date.AddDays(1);
        var mainCurrency = await GetMainCurrencyAsync(factory, userId, ct);

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
                HasAccountFilter = request.AccountIds is { Count: > 0 },
                AccountIds = request.AccountIds?.ToArray()
            }, ct)).ToList();

        currencies = currencies.OrderByDescending(c => Math.Abs(c.Balance)).ToList();

        var approximateIncome = 0m;
        var approximateExpenses = 0m;
        var accountRows = (await factory.QueryListAsync<AccountCurrencyRow>(
            @"SELECT ""Id"", ""Currency"" FROM ""Accounts"" WHERE ""UserId"" = @UserId",
            new { UserId = userId }, ct)).ToList();

        foreach (var t in await GetMonthlyTransactionsAsync(factory, userId, startDate, endDate, request.AccountIds, ct))
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

        return new GetTransactionsSummaryResponse(currencies, mainCurrency,
            approximateIncome, approximateExpenses);
    }

    private static async Task<List<TxFlowRow>> GetMonthlyTransactionsAsync(IDbConnectionFactory factory,
        Guid userId, DateTime startDate, DateTime endDate, IReadOnlyList<Guid>? accountIds, CancellationToken ct)
        => (await factory.QueryListAsync<TxFlowRow>(
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

    private static async Task<string> GetMainCurrencyAsync(IDbConnectionFactory factory, Guid userId, CancellationToken ct)
        => await factory.QuerySingleOrDefaultAsync<string>(
            @"SELECT ""MainCurrency"" FROM ""UserSettings"" WHERE ""UserId"" = @UserId",
            new { UserId = userId }, ct) ?? "USD";

    private record AccountCurrencyRow(Guid Id, string Currency);
    private record TxFlowRow(Guid Id, Guid AccountId, decimal Amount, string Currency, decimal ExchangeRate, int Type);
}
