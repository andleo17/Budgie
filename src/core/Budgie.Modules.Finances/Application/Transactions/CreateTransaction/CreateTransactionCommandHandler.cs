using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Transactions.CreateTransaction;

public record TransactionParticipantDto(Guid PersonId, decimal ContributedAmount,
    decimal RemainingAmount, bool IsTrackingOnly, Guid? DestinationAccountId);

public record CreateTransactionCommand(
    Guid AccountId, Guid? CategoryId, decimal Amount, string Currency,
    decimal ExchangeRate, DateTime Date, string? Notes, TransactionType Type,
    List<TransactionParticipantDto>? Participants, int Installments,
    Guid? ParentTransactionId, bool IsInitialBalance) : IRequest<ErrorOr<Guid>>;

internal sealed class CreateTransactionCommandHandler(
    IAccountRepository accountRepository,
    ITransactionRepository txRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<CreateTransactionCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(CreateTransactionCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var account = await accountRepository.GetByIdAsync(request.AccountId, userId, ct);
        if (account is null) return Error.NotFound("Account.NotFound", "Account not found.");

        var tx = Transaction.Create(userId, request.AccountId, request.CategoryId,
            request.Amount, request.Currency, request.ExchangeRate, request.Date, request.Notes,
            request.Type, request.Installments, parentTransactionId: request.ParentTransactionId,
            isInitialBalance: request.IsInitialBalance);

        await txRepository.AddAsync(tx, ct);

        if (request.Participants is { Count: > 0 })
            await InsertParticipants(tx, request, userId, ct);

        var delta = request.Type is TransactionType.Income or TransactionType.Repayment
            ? request.Amount : -request.Amount;
        await accountRepository.UpdateBalanceAsync(request.AccountId, account.Balance.Amount + delta, ct);

        return tx.Id;
    }

    private async Task InsertParticipants(Transaction tx, CreateTransactionCommand request, Guid userId, CancellationToken ct)
    {
        foreach (var p in request.Participants!)
        {
            var participant = tx.CreateParticipant(p.PersonId, p.ContributedAmount,
                p.RemainingAmount, p.IsTrackingOnly, p.DestinationAccountId);
            await txRepository.AddParticipantAsync(participant, ct);

            if (!p.IsTrackingOnly && p.RemainingAmount > 0 && request.Type != TransactionType.Loan)
            {
                var childTx = Transaction.Create(userId, request.AccountId, null,
                    p.RemainingAmount, request.Currency, request.ExchangeRate, request.Date,
                    request.Notes, TransactionType.Loan, 1, parentTransactionId: tx.Id);
                await txRepository.AddAsync(childTx, ct);

                var childP = childTx.CreateParticipant(p.PersonId, p.RemainingAmount, p.RemainingAmount);
                await txRepository.AddParticipantAsync(childP, ct);
            }
        }
    }
}
