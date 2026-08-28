using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Transactions.DeleteTransaction;

public record DeleteTransactionCommand(Guid Id) : IRequest<ErrorOr<Success>>;

internal sealed class DeleteTransactionCommandHandler(
    ITransactionRepository txRepository,
    IAccountRepository accountRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<DeleteTransactionCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(DeleteTransactionCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var tx = await txRepository.GetByIdForDeleteAsync(request.Id, userId, ct);
        if (tx is null) return Error.NotFound("Transaction.NotFound", "Transaction not found.");
        if (tx.IsInitialBalance) return Error.Validation("Transaction.CannotDeleteInitialBalance", "Cannot delete initial balance.");

        if (tx.Type == TransactionType.Repayment)
        {
            var details = await txRepository.GetRepaymentDetailsAsync(request.Id, ct);
            foreach (var d in details)
            {
                await txRepository.RestoreParticipantRemainingAsync(d.RepaidParticipantId, d.Amount, ct);
                await txRepository.RestoreParentParticipantRemainingAsync(d.RepaidParticipantId, d.Amount, ct);
            }
            await txRepository.DeleteRepaymentDetailsAsync(request.Id, ct);
        }

        var account = await accountRepository.GetByIdAsync(tx.AccountId, userId, ct);
        if (account is null) return Error.NotFound("Account.NotFound", "Account not found.");

        if (tx.ParentTransactionId.HasValue)
        {
            await DeleteChild(account, tx, ct);
        }
        else
        {
            await DeleteParent(account, tx, ct);
        }

        await txRepository.DeleteParticipantAsync(tx.Id, ct);
        await txRepository.DeleteTransactionAsync(tx.Id, ct);

        await accountRepository.UpdateBalanceAsync(account.UserId, account.Id, account.Balance.Amount, ct);

        return Result.Success;
    }

    private async Task DeleteParent(Account account, TransactionInfo parent, CancellationToken ct)
    {
        var children = await txRepository.GetChildTransactionsForDeleteAsync(parent.Id, ct);

        foreach (var child in children)
        {
            if (child.Type == TransactionType.Income)
                account.RevertTransaction(ToMoney(child), child.ExchangeRate, child.Installments, child.Type);
        }

        await txRepository.DeleteChildParticipantsAsync(parent.Id, ct);
        await txRepository.DeleteChildTransactionsAsync(parent.Id, ct);

        account.RevertTransaction(ToMoney(parent), parent.ExchangeRate, parent.Installments, parent.Type);
    }

    private async Task DeleteChild(Account account, TransactionInfo child, CancellationToken ct)
    {
        if (child.Type == TransactionType.Income)
            account.RevertTransaction(ToMoney(child), child.ExchangeRate, child.Installments, child.Type);

        var personId = await txRepository.GetFirstParticipantPersonIdAsync(child.Id, ct);
        if (personId.HasValue && child.ParentTransactionId.HasValue)
            await txRepository.DeleteParentParticipantAsync(child.ParentTransactionId.Value, personId.Value, ct);
    }

    private static Money ToMoney(TransactionInfo t)
        => Money.FromPositive(t.Amount, CurrencyCode.From(t.Currency));
}
