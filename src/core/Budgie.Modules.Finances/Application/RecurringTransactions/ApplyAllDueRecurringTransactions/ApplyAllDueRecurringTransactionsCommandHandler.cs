using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.RecurringTransactions.ApplyAllDueRecurringTransactions;

internal sealed class ApplyAllDueRecurringTransactionsCommandHandler(
    IAccountRepository accountRepository,
    IRecurringTransactionRepository recurringTransactionRepository,
    ITransactionRepository txRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<ApplyAllDueRecurringTransactionsCommand, ErrorOr<List<Guid>>>
{
    public async ValueTask<ErrorOr<List<Guid>>> Handle(ApplyAllDueRecurringTransactionsCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var now = DateTime.UtcNow;

        var due = await recurringTransactionRepository.GetDueByUserIdAsync(userId, ct);

        var createdIds = new List<Guid>();

        foreach (var template in due)
        {
            var account = await accountRepository.GetByIdAsync(template.AccountId, userId, ct);
            if (account is null) continue;

            var tx = Transaction.Create(
                userId,
                template.AccountId,
                template.CategoryId,
                template.Amount.Amount,
                template.Amount.Currency.Code,
                template.ExchangeRate,
                now,
                template.Notes,
                template.Type,
                template.Installments);

            foreach (var p in template.Participants)
            {
                tx.CreateParticipant(p.PersonId, p.ContributedAmount.Amount, p.RemainingAmount.Amount, p.IsTrackingOnly, p.DestinationAccountId);
            }

            account.ApplyTransaction(template.Amount, template.ExchangeRate, template.Installments, template.Type);
            await txRepository.AddAsync(tx, ct);
            foreach (var p in tx.Participants)
                await txRepository.AddParticipantAsync(p, ct);

            template.Apply();
            await recurringTransactionRepository.UpdateApplyStateAsync(template, ct);
            await accountRepository.UpdateBalanceAsync(account.UserId, account.Id, account.Balance.Amount, ct);

            createdIds.Add(tx.Id);
        }

        return createdIds;
    }
}
