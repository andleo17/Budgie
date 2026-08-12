using Budgie.Shared.Abstractions;
using Budgie.Shared.Infrastructure;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Reports.GetCategorySpending;

internal sealed class GetCategorySpendingQueryHandler(
    IDbConnectionFactory factory,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetCategorySpendingQuery, ErrorOr<List<CategorySpending>>>
{
    public async ValueTask<ErrorOr<List<CategorySpending>>> Handle(GetCategorySpendingQuery request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var startDate = Guard.NormalizeDate(request.StartDate);
        var endDate = Guard.NormalizeDate(request.EndDate).Date.AddDays(1);

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

    private record SpendingRow(Guid? CategoryId, string CategoryName, string? Color, decimal TotalAmount);
}
