using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Modules.Finances.Infrastructure.Logging;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.Accounts.UpdateAccount;

internal sealed class UpdateAccountCommandHandler(
    FinancesDbContext dbContext,
    ICurrentUserProvider currentUserProvider,
    ILogger<UpdateAccountCommandHandler> logger)
    : IRequestHandler<UpdateAccountCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(UpdateAccountCommand request, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);

        if (account is null)
        {
            return Error.NotFound("Account.NotFound", "Account not found.");
        }

        var oldCreditLimit = account.CreditCardSettings?.CreditLimit?.Amount ?? 0m;

        CreditCardSettings? creditCardSettings = null;
        if (account.Type == AccountType.Credit)
        {
            creditCardSettings = new CreditCardSettings(
                request.CreditLimit ?? 0m,
                request.CreditConsumptionMode ?? CreditConsumptionMode.Total,
                request.BillingCycleDay,
                request.PaymentDueDay,
                request.AnnualInterestRate ?? 0m,
                request.BillingCycleDayInclusive,
                CurrencyCode.From(account.Balance.Currency),
                request.InterestCalculationMethod ?? Budgie.Shared.Enums.InterestCalculationMethod.FrenchAmortization);
        }

        account.Update(request.Name, creditCardSettings);

        if (account.Type == AccountType.Credit && request.CreditLimit > oldCreditLimit)
        {
            var increase = request.CreditLimit!.Value - oldCreditLimit;
            var limitTx = Transaction.Create(
                currentUserProvider.GetUserId(),
                account.Id,
                null,
                increase,
                account.Balance.Currency,
                1m,
                DateTime.UtcNow,
                $"Credit limit increased from {oldCreditLimit} to {request.CreditLimit}",
                TransactionType.Loan);

            dbContext.Transactions.Add(limitTx);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        LogMessages.AccountUpdated(logger, request.Id);

        return Result.Success;
    }
}
