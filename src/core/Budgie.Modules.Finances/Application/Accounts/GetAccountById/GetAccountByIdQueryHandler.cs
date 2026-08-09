using Budgie.Modules.Finances.Infrastructure.Database;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.Accounts.GetAccountById;

internal sealed class GetAccountByIdQueryHandler(FinancesDbContext dbContext)
    : IRequestHandler<GetAccountByIdQuery, ErrorOr<AccountResponse>>
{
    public async ValueTask<ErrorOr<AccountResponse>> Handle(GetAccountByIdQuery request, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts
            .AsNoTracking()
            .Where(a => a.Id == request.Id)
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
            .FirstOrDefaultAsync(cancellationToken);

        if (account is null)
            return Error.NotFound("Account.NotFound", "Account not found.");

        return new AccountResponse(
            account.Id,
            account.Name,
            account.Currency,
            account.Balance,
            account.Type,
            account.CreditCardSettings?.CreditLimit?.Amount,
            account.CreditCardSettings?.CreditConsumptionMode,
            account.CreatedAt,
            account.CreditCardSettings?.BillingCycleDay,
            account.CreditCardSettings?.PaymentDueDay,
            account.CreditCardSettings?.AnnualInterestRate != null ? (decimal?)account.CreditCardSettings.AnnualInterestRate.Value : null,
            account.CreditCardSettings?.BillingCycleDayInclusive ?? true,
            account.CreditCardSettings?.InterestCalculationMethod);
    }
}
