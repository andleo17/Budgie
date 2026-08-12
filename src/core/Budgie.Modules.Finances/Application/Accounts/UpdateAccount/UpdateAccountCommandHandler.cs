using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Accounts.UpdateAccount;

public record UpdateAccountCommand(
    Guid Id, string Name, decimal? CreditLimit,
    CreditConsumptionMode? CreditConsumptionMode = null,
    int? BillingCycleDay = null, int? PaymentDueDay = null,
    decimal? AnnualInterestRate = null, bool BillingCycleDayInclusive = true,
    InterestCalculationMethod? InterestCalculationMethod = null) : IRequest<ErrorOr<Success>>;

internal sealed class UpdateAccountCommandHandler(
    IAccountRepository accountRepository,
    ITransactionRepository txRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<UpdateAccountCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(UpdateAccountCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var account = await accountRepository.GetByIdAsync(request.Id, userId, ct);
        if (account is null) return Error.NotFound("Account.NotFound", "Account not found.");

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
        await accountRepository.UpdateAsync(account, ct);

        if (account.Type == AccountType.Credit && request.CreditLimit > oldCreditLimit)
        {
            var increase = request.CreditLimit!.Value - oldCreditLimit;
            var limitTx = Transaction.Create(userId, account.Id, null, increase,
                account.Balance.Currency, 1m, DateTime.UtcNow,
                $"Credit limit increased from {oldCreditLimit} to {request.CreditLimit}",
                TransactionType.Loan);
            await txRepository.AddAsync(limitTx, ct);
        }

        return Result.Success;
    }
}
