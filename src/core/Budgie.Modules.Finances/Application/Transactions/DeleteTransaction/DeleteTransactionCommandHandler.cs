using Budgie.Modules.Finances.Application;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Transactions.DeleteTransaction;

public record DeleteTransactionCommand(Guid Id) : IRequest<ErrorOr<Success>>;

internal sealed class DeleteTransactionCommandHandler(
    ITransactionRepository txRepository,
    IAccountRepository accountRepository)
    : IRequestHandler<DeleteTransactionCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(DeleteTransactionCommand request, CancellationToken ct)
    {
        var tx = await txRepository.GetByIdForDeleteAsync(request.Id, ct);
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

        var account = await accountRepository.GetByIdRawAsync(tx.AccountId, ct);
        var revertAmount = tx.Type is TransactionType.Income or TransactionType.Repayment ? tx.Amount : -tx.Amount;

        await txRepository.DeleteParticipantAsync(tx.Id, ct);
        await txRepository.DeleteChildParticipantsAsync(tx.Id, ct);
        await txRepository.DeleteChildTransactionsAsync(tx.Id, ct);
        await txRepository.DeleteTransactionAsync(tx.Id, ct);

        if (account is not null)
            await accountRepository.UpdateBalanceAsync(account.Id, account.Balance.Amount - revertAmount, ct);

        return Result.Success;
    }
}
