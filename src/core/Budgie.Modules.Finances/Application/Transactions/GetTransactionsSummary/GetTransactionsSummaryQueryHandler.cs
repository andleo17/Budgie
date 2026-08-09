using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using Budgie.Modules.Finances.Infrastructure.Database;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.Transactions.GetTransactionsSummary;

internal sealed class GetTransactionsSummaryQueryHandler(
    FinancesDbContext dbContext,
    IUserSettingsProvider settingsProvider)
    : IRequestHandler<GetTransactionsSummaryQuery, ErrorOr<GetTransactionsSummaryResponse>>
{
    public async ValueTask<ErrorOr<GetTransactionsSummaryResponse>> Handle(GetTransactionsSummaryQuery request, CancellationToken cancellationToken)
    {
        var mainCurrency = await settingsProvider.GetMainCurrencyAsync();

        var query = dbContext.Transactions
            .Include(t => t.Account)
            .Where(t => t.ParentTransactionId == null)
            .AsQueryable();

        if (request.AccountIds is { Count: > 0 })
            query = query.Where(t => request.AccountIds.Contains(t.AccountId));

        var startDate = Guard.NormalizeDate(request.StartDate);
        var endDate = Guard.NormalizeDate(request.EndDate);
        query = query.Where(t => t.Date >= startDate && t.Date < endDate.Date.AddDays(1));

        var transactions = await query.ToListAsync(cancellationToken);

        var byCurrency = transactions
            .GroupBy(t => t.Account.Balance.Currency)
            .Select(g =>
            {
                var income = g
                    .Where(t => t.Type is TransactionType.Income or TransactionType.Repayment)
                    .Sum(t => t.Amount.Currency != t.Account.Balance.Currency ? t.Amount.Amount * t.ExchangeRate : t.Amount.Amount);

                var expenses = g
                    .Where(t => t.Type is TransactionType.Expense or TransactionType.Transfer or TransactionType.Loan)
                    .Sum(t => t.Amount.Currency != t.Account.Balance.Currency ? t.Amount.Amount * t.ExchangeRate : t.Amount.Amount);

                return new CurrencySummary(income, expenses, income - expenses, g.Key);
            })
            .OrderByDescending(c => Math.Abs(c.Balance))
            .ToList();

        var approximateIncome = 0m;
        var approximateExpenses = 0m;

        foreach (var t in transactions)
        {
            var isIncome = t.Type is TransactionType.Income or TransactionType.Repayment;
            var isExpense = t.Type is TransactionType.Expense or TransactionType.Transfer or TransactionType.Loan;

            if (!isIncome && !isExpense) continue;

            var inMain = t.Account.Balance.Currency == mainCurrency
                ? t.Amount.Amount * t.ExchangeRate
                : t.Amount.Currency == mainCurrency
                    ? t.Amount.Amount
                    : t.Amount.Amount * t.ExchangeRate;

            if (isIncome) approximateIncome += inMain;
            if (isExpense) approximateExpenses += inMain;
        }

        return new GetTransactionsSummaryResponse(byCurrency, mainCurrency, approximateIncome, approximateExpenses);
    }
}
