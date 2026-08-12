using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Accounts.CreateAccount;

public record CreateAccountCommand(
    string Name, string Currency, decimal InitialBalance, AccountType Type,
    decimal? CreditLimit, CreditConsumptionMode? CreditConsumptionMode = null,
    int? BillingCycleDay = null, int? PaymentDueDay = null, decimal? AnnualInterestRate = null,
    bool BillingCycleDayInclusive = true, InterestCalculationMethod? InterestCalculationMethod = null,
    DateTime? CreatedAt = null) : IRequest<ErrorOr<Guid>>;

internal sealed class CreateAccountCommandHandler(
    IAccountRepository accountRepository,
    ITransactionRepository txRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<CreateAccountCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(CreateAccountCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();

        CreditCardSettings? creditCardSettings = null;
        if (request.Type == AccountType.Credit)
        {
            creditCardSettings = new CreditCardSettings(
                request.CreditLimit ?? 0m,
                request.CreditConsumptionMode ?? CreditConsumptionMode.Total,
                request.BillingCycleDay,
                request.PaymentDueDay,
                request.AnnualInterestRate ?? 0m,
                request.BillingCycleDayInclusive,
                CurrencyCode.From(request.Currency),
                request.InterestCalculationMethod ?? Budgie.Shared.Enums.InterestCalculationMethod.FrenchAmortization);
        }

        var account = Account.Create(userId, request.Name, request.Currency, 0m,
            request.Type, creditCardSettings, request.CreatedAt);

        await accountRepository.AddAsync(account, ct);

        var balanceDelta = 0m;

        if (request.Type == AccountType.Credit && request.CreditLimit > 0)
        {
            var limitAmount = request.CreditLimit.Value;
            var limitTx = Transaction.Create(userId, account.Id, null, limitAmount,
                account.Balance.Currency, 1m, account.CreatedAt, null,
                TransactionType.Income);
            await txRepository.AddAsync(limitTx, ct);
            balanceDelta += limitAmount;
        }

        if (request.InitialBalance != 0)
        {
            var txType = request.Type == AccountType.Credit
                ? TransactionType.Expense
                : request.InitialBalance > 0 ? TransactionType.Income : TransactionType.Expense;
            var absAmount = Math.Abs(request.InitialBalance);

            var tx = Transaction.Create(userId, account.Id, SystemCategoryIds.InitialBalance,
                absAmount, account.Balance.Currency, 1m, account.CreatedAt, null,
                txType, isInitialBalance: true);
            await txRepository.AddAsync(tx, ct);

            balanceDelta += txType is TransactionType.Income ? absAmount : -absAmount;
        }

        if (balanceDelta != 0)
        {
            var accountForBalance = await accountRepository.GetByIdRawAsync(account.Id, ct);
            if (accountForBalance is not null)
                await accountRepository.UpdateBalanceAsync(account.Id, accountForBalance.Balance.Amount + balanceDelta, ct);
        }

        return account.Id;
    }
}
