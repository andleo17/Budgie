using Budgie.Shared.Abstractions;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.Reports.GetCategorySpending;

internal sealed class GetCategorySpendingQueryHandler(FinancesDbContext dbContext)
    : IRequestHandler<GetCategorySpendingQuery, ErrorOr<List<CategorySpending>>>
{
    public async ValueTask<ErrorOr<List<CategorySpending>>> Handle(GetCategorySpendingQuery request, CancellationToken cancellationToken)
    {
        var startDate = Guard.NormalizeDate(request.StartDate);
        var endDate = Guard.NormalizeDate(request.EndDate);

        var expenses = await dbContext.Transactions
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.Participants)
            .Where(t => t.Date >= startDate && t.Date < endDate.Date.AddDays(1) && t.Type == TransactionType.Expense)
            .ToListAsync(cancellationToken);

        var spendingByCategory = expenses
            .GroupBy(t => new { t.CategoryId, Name = t.Category?.Name ?? "Uncategorized", t.Category?.Color })
            .Select(g =>
            {
                var totalAmount = g.Sum(t =>
                {
                    var othersContribution = t.Participants.Sum(p => p.ContributedAmount.Amount);
                    return t.Amount.Amount - othersContribution;
                });

                return new CategorySpending(
                    g.Key.CategoryId ?? Guid.Empty,
                    g.Key.Name,
                    g.Key.Color,
                    totalAmount,
                    0);
            })
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
}
