using Budgie.Shared.Abstractions;
using Budgie.Shared.Infrastructure;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Reports.GetTransactionsSummary;

public record GetTransactionsSummaryQuery(DateTime? StartDate = null, DateTime? EndDate = null,
    string? AccountIds = null) : IRequest<ErrorOr<GetTransactionsSummaryResponse>>;

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
        var startDate = request.StartDate ?? DateTime.UtcNow.Date.AddDays(-30);
        var endDate = request.EndDate ?? DateTime.UtcNow.Date.AddDays(1);
        var mainCurrency = await GetMainCurrencyAsync(factory, userId, ct);

        var currencies = (await factory.QueryListAsync<CurrencySummary>(
            @"SELECT
                COALESCE(SUM(CASE WHEN t.""Type"" = 0 OR t.""Type"" = 4 THEN t.""Amount"" ELSE 0 END), 0) AS Income,
                COALESCE(SUM(CASE WHEN t.""Type"" = 1 OR t.""Type"" = 3 THEN t.""Amount"" ELSE 0 END), 0) AS Expenses,
                COALESCE(SUM(CASE WHEN t.""Type"" = 0 OR t.""Type"" = 4 THEN t.""Amount"" ELSE -t.""Amount"" END), 0) AS Balance,
                t.""Currency"" AS Currency
            FROM ""Transactions"" t
            WHERE t.""UserId"" = @UserId
              AND t.""Date"" >= @StartDate AND t.""Date"" < @EndDate
              AND t.""ParentTransactionId"" IS NULL
            GROUP BY t.""Currency""",
            new { UserId = userId, StartDate = startDate, EndDate = endDate }, ct)).ToList();

        var approximateIncome = currencies.Where(c => c.Currency == mainCurrency).Sum(c => c.Income);
        var approximateExpenses = currencies.Where(c => c.Currency == mainCurrency).Sum(c => c.Expenses);

        return new GetTransactionsSummaryResponse(currencies, mainCurrency,
            approximateIncome, approximateExpenses);
    }

    private static async Task<string> GetMainCurrencyAsync(IDbConnectionFactory factory, Guid userId, CancellationToken ct)
        => await factory.QuerySingleOrDefaultAsync<string>(
            @"SELECT ""MainCurrency"" FROM ""UserSettings"" WHERE ""UserId"" = @UserId",
            new { UserId = userId }, ct) ?? "USD";
}
