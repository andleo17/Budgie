using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.RecurringTransactions.ApplyRecurringTransaction;

internal sealed class ApplyRecurringTransactionCommandHandler(
    IAccountRepository accountRepository,
    IRecurringTransactionRepository recurringTransactionRepository,
    ITransactionRepository txRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<ApplyRecurringTransactionCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(ApplyRecurringTransactionCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();

        var template = await recurringTransactionRepository.GetByIdAsync(request.Id, userId, ct);
        if (template is null)
            return Error.NotFound("RecurringTransaction.NotFound", "Template not found.");

        var account = await accountRepository.GetByIdAsync(template.AccountId, userId, ct);
        if (account is null)
            return Error.NotFound("Account.NotFound", "Account not found.");

        var tx = Transaction.Create(
            userId,
            template.AccountId,
            template.CategoryId,
            template.Amount.Amount,
            template.Amount.Currency.Code,
            template.ExchangeRate,
            DateTime.UtcNow,
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
        await accountRepository.UpdateBalanceAsync(account.Id, account.Balance.Amount, ct);

        return tx.Id;
    }
}
