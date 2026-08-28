using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Modules.Finances.Domain.Events;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Shared.Enums;
using Mediator;

namespace Budgie.Modules.Finances.Application.Accounts.EventHandlers;

internal sealed class RecordInitialBalanceOnAccountCreated(
    ITransactionRepository txRepository,
    IAccountRepository accountRepository)
    : INotificationHandler<AccountCreatedEvent>
{
    public async ValueTask Handle(AccountCreatedEvent notification, CancellationToken ct)
    {
        var balanceDelta = 0m;

        if (notification.Type == AccountType.Credit && notification.CreditLimit > 0)
        {
            var limitTx = Transaction.Create(notification.UserId, notification.AccountId, null,
                notification.CreditLimit!.Value, notification.Currency, 1m,
                notification.ControlStartDate, null, TransactionType.Income);
            await txRepository.AddAsync(limitTx, ct);
            balanceDelta += notification.CreditLimit.Value;
        }

        if (notification.InitialBalance != 0)
        {
            var txType = notification.Type == AccountType.Credit
                ? TransactionType.Expense
                : notification.InitialBalance > 0 ? TransactionType.Income : TransactionType.Expense;
            var absAmount = Math.Abs(notification.InitialBalance);

            var tx = Transaction.Create(notification.UserId, notification.AccountId,
                SystemCategoryIds.InitialBalance, absAmount, notification.Currency, 1m,
                notification.ControlStartDate, null, txType, isInitialBalance: true);
            await txRepository.AddAsync(tx, ct);

            balanceDelta += txType is TransactionType.Income ? absAmount : -absAmount;
        }

        if (balanceDelta != 0)
        {
            var account = await accountRepository.GetByIdRawAsync(notification.AccountId, ct);
            if (account is not null)
                await accountRepository.UpdateBalanceAsync(account.UserId, account.Id, account.Balance.Amount + balanceDelta, ct);
        }
    }
}
