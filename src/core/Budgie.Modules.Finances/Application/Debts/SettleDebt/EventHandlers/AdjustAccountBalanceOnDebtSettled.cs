using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Domain.Events;
using Mediator;

namespace Budgie.Modules.Finances.Application.Debts.SettleDebt.EventHandlers;

internal sealed class AdjustAccountBalanceOnDebtSettled(
    IAccountRepository accountRepository)
    : INotificationHandler<DebtSettledEvent>
{
    public async ValueTask Handle(DebtSettledEvent notification, CancellationToken ct)
    {
        var account = await accountRepository.GetByIdRawAsync(notification.DestinationAccountId, ct);
        if (account is null) return;

        await accountRepository.UpdateBalanceAsync(
            account.Id, account.Balance.Amount + notification.Amount, ct);
    }
}
