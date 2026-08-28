using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Domain.Events;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Shared.Enums;
using Mediator;

namespace Budgie.Modules.Finances.Application.Transactions.EventHandlers;

internal sealed class RecordSplitDebtsOnTransactionCreated(
    ITransactionRepository txRepository,
    IAccountRepository accountRepository)
    : INotificationHandler<TransactionCreatedEvent>
{
    public async ValueTask Handle(TransactionCreatedEvent notification, CancellationToken ct)
    {
        if (notification.Participants is null or { Count: 0 })
            return;

        foreach (var p in notification.Participants)
        {
            if (p.IsTrackingOnly) continue;

            if (p.RemainingAmount > 0 && notification.Type != TransactionType.Loan)
            {
                // Create child virtual loan transaction for tracking
                var childTx = Transaction.Create(
                    notification.UserId,
                    notification.AccountId,
                    null,
                    p.RemainingAmount,
                    notification.Currency,
                    notification.ExchangeRate,
                    notification.Date,
                    "Split expense share",
                    TransactionType.Loan,
                    1,
                    parentTransactionId: notification.TransactionId);

                await txRepository.AddAsync(childTx, ct);

                var childP = childTx.CreateParticipant(p.PersonId, p.RemainingAmount, p.RemainingAmount);
                await txRepository.AddParticipantAsync(childP, ct);
            }
            else if (p.RemainingAmount == 0 && p.DestinationAccountId.HasValue && p.DestinationAccountId.Value != notification.AccountId)
            {
                // Immediate settlement into a secondary account
                var destAccount = await accountRepository.GetByIdRawAsync(p.DestinationAccountId.Value, ct);
                if (destAccount is not null)
                {
                    var childTx = Transaction.Create(
                        notification.UserId,
                        p.DestinationAccountId.Value,
                        null,
                        p.ContributedAmount,
                        notification.Currency,
                        notification.ExchangeRate,
                        notification.Date,
                        "Immediate split settlement",
                        TransactionType.Income,
                        1,
                        parentTransactionId: notification.TransactionId);

                    await txRepository.AddAsync(childTx, ct);

                    var childP = childTx.CreateParticipant(p.PersonId, p.ContributedAmount, 0, false, p.DestinationAccountId);
                    await txRepository.AddParticipantAsync(childP, ct);

                    await accountRepository.UpdateBalanceAsync(
                        destAccount.UserId,
                        destAccount.Id,
                        destAccount.Balance.Amount + p.ContributedAmount,
                        ct);
                }
            }
        }
    }
}
