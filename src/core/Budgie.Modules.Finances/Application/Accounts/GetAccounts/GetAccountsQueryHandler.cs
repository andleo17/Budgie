using Budgie.Modules.Finances.Infrastructure.Database;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.Accounts.GetAccounts;

internal sealed class GetAccountsQueryHandler(FinancesDbContext dbContext)
    : IRequestHandler<GetAccountsQuery, ErrorOr<List<AccountResponse>>>
{
    public async Task<ErrorOr<List<AccountResponse>>> Handle(GetAccountsQuery request, CancellationToken cancellationToken)
    {
        var raw = await dbContext.Accounts
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .Select(a => new
            {
                a.Id,
                Name = a.Name,
                Currency = a.Balance.Currency,
                Balance = a.Balance.Amount,
                a.Type,
                a.CreditCardSettings,
                a.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var accounts = raw.Select(a => new AccountResponse(
            a.Id,
            a.Name,
            a.Currency,
            a.Balance,
            a.Type,
            a.CreditCardSettings?.CreditLimit?.Amount,
            a.CreditCardSettings?.CreditConsumptionMode,
            a.CreatedAt,
            a.CreditCardSettings?.BillingCycleDay,
            a.CreditCardSettings?.PaymentDueDay,
            a.CreditCardSettings?.AnnualInterestRate != null ? (decimal?)a.CreditCardSettings.AnnualInterestRate.Value : null,
            a.CreditCardSettings?.BillingCycleDayInclusive ?? true,
            a.CreditCardSettings?.InterestCalculationMethod)).ToList();

        return accounts;
    }
}
