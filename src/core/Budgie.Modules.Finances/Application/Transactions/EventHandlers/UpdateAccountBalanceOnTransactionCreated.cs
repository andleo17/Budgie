using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Domain.Events;
using Budgie.Shared.Enums;
using Mediator;

namespace Budgie.Modules.Finances.Application.Transactions.EventHandlers;

internal sealed class UpdateAccountBalanceOnTransactionCreated(
    IAccountRepository accountRepository)
    : INotificationHandler<TransactionCreatedEvent>
{
    public async ValueTask Handle(TransactionCreatedEvent notification, CancellationToken ct)
    {
        var account = await accountRepository.GetByIdRawAsync(notification.AccountId, ct);
        if (account is null) return;

        var delta = notification.Type switch
        {
            TransactionType.Income or TransactionType.Repayment => notification.Amount,
            _ => -notification.Amount
        };

        var participantImmediateDelta = 0m;
        if (notification.Participants is { Count: > 0 })
        {
            foreach (var p in notification.Participants)
            {
                if (!p.IsTrackingOnly && p.RemainingAmount == 0 && p.DestinationAccountId == notification.AccountId)
                {
                    participantImmediateDelta += p.ContributedAmount;
                }
            }
        }

        var newBalance = account.Balance.Amount + delta + participantImmediateDelta;
        await accountRepository.UpdateBalanceAsync(account.UserId, notification.AccountId, newBalance, ct);
    }
}
