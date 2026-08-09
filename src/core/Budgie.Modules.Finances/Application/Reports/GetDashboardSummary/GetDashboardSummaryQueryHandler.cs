using Budgie.Shared.Abstractions;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.Reports.GetDashboardSummary;

internal sealed class GetDashboardSummaryQueryHandler(FinancesDbContext dbContext)
    : IRequestHandler<GetDashboardSummaryQuery, ErrorOr<DashboardSummary>>
{
    public async ValueTask<ErrorOr<DashboardSummary>> Handle(GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        var accounts = await dbContext.Accounts.AsNoTracking().ToListAsync(cancellationToken);

        var totalBalance = accounts.Sum(a => a.Balance.Amount);

        var firstDayOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);

        var monthlyTransactions = await dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.Date >= firstDayOfMonth && t.ParentTransactionId == null)
            .ToListAsync(cancellationToken);

        var monthlyIncome = monthlyTransactions
            .Where(t => t.Type == TransactionType.Income || t.Type == TransactionType.Repayment)
            .Sum(t => t.Amount.Amount);

        var monthlyExpenses = monthlyTransactions
            .Where(t => t.Type == TransactionType.Expense || t.Type == TransactionType.Loan)
            .Sum(t => t.Amount.Amount);

        return new DashboardSummary(
            totalBalance,
            monthlyIncome,
            monthlyExpenses,
            monthlyIncome - monthlyExpenses,
            request.MainCurrency);
    }
}
