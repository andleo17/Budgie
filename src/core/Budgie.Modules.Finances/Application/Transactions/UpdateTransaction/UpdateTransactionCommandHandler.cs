using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Application.Transactions.CreateTransaction;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;
using System.Globalization;

namespace Budgie.Modules.Finances.Application.Transactions.UpdateTransaction;

public record UpdateTransactionCommand(
    Guid Id, Guid? CategoryId, decimal Amount, string Currency, decimal ExchangeRate,
    DateTime Date, string? Notes, TransactionType Type, int Installments,
    List<TransactionParticipantDto>? Participants = null,
    Guid? ParentTransactionId = null) : IRequest<ErrorOr<Success>>;

internal sealed class UpdateTransactionCommandHandler(
    ITransactionRepository txRepository,
    IAccountRepository accountRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<UpdateTransactionCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(UpdateTransactionCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var existing = await txRepository.GetByIdForUpdateAsync(request.Id, userId, ct);
        if (existing is null) return Error.NotFound("Transaction.NotFound", "Transaction not found.");

        var account = await accountRepository.GetByIdAsync(existing.AccountId, userId, ct);
        if (account is null) return Error.NotFound("Account.NotFound", "Account not found.");

        var normalizedDate = request.Date;

        if (existing.IsInitialBalance && normalizedDate != existing.Date)
            return Error.Validation("Transaction.InitialBalanceDateLocked", "Initial balance transaction date cannot be modified.");

        if (normalizedDate < account.ControlStartDate.Date)
        {
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
                ? Error.Validation("Transaction.BeforeControlStartDate", $"Transaction date ({normalizedDate:yyyy-MM-dd}) cannot precede the account control start date ({account.ControlStartDate:yyyy-MM-dd}).")
                : Error.Validation("Transaction.BeforeControlStartDate", $"La fecha de la transacción ({normalizedDate:yyyy-MM-dd}) no puede ser anterior a la fecha de control de la cuenta ({account.ControlStartDate:yyyy-MM-dd}).");
        }

        if (request.Participants is { Count: > 1 } &&
            request.Participants.Count(p => p.RemainingAmount == 0 && !p.IsTrackingOnly && p.DestinationAccountId.HasValue) > 1)
            return Error.Validation("Transaction.TooManyRepaid", "Only one participant can be repaid per transaction.");

        // Revert old impact (including child INCOMEs)
        await RevertChildrenAsync(account, existing.Id, ct);
        account.RevertTransaction(ToMoney(existing), existing.ExchangeRate, existing.Installments, existing.Type);

        // Delete old children + participants
        await txRepository.DeleteChildParticipantsAsync(existing.Id, ct);
        await txRepository.DeleteChildTransactionsAsync(existing.Id, ct);
        await txRepository.DeleteParticipantAsync(existing.Id, ct);

        // Build updated transaction
        var tx = Transaction.Create(userId, existing.AccountId, request.CategoryId,
            request.Amount, request.Currency, request.ExchangeRate, normalizedDate,
            request.Notes, request.Type, request.Installments,
            parentTransactionId: request.ParentTransactionId,
            isInitialBalance: existing.IsInitialBalance,
            installmentGroupId: existing.InstallmentGroupId,
            installmentNumber: existing.InstallmentNumber,
            totalInstallments: existing.TotalInstallments,
            interestRate: existing.InterestRate);
        tx.Id = existing.Id;

        await txRepository.UpdateAsync(tx, ct);

        var participantDelta = 0m;
        if (request.Participants is { Count: > 0 })
        {
            foreach (var p in request.Participants)
            {
                var participant = tx.CreateParticipant(p.PersonId, p.ContributedAmount,
                    p.RemainingAmount, p.IsTrackingOnly, p.DestinationAccountId);
                await txRepository.AddParticipantAsync(participant, ct);

                if (p.IsTrackingOnly) continue;

                if (p.RemainingAmount > 0 && request.Type != TransactionType.Loan)
                {
                    var childTx = Transaction.Create(userId, existing.AccountId, null,
                        p.RemainingAmount, request.Currency, request.ExchangeRate, normalizedDate,
                        request.Notes, TransactionType.Loan, 1, parentTransactionId: existing.Id);
                    await txRepository.AddAsync(childTx, ct);
                    var childP = childTx.CreateParticipant(p.PersonId, p.RemainingAmount, p.RemainingAmount);
                    await txRepository.AddParticipantAsync(childP, ct);
                }
                else if (p.RemainingAmount == 0 && p.DestinationAccountId.HasValue)
                {
                    var childTx = Transaction.Create(userId, existing.AccountId, null,
                        p.ContributedAmount, request.Currency, request.ExchangeRate, normalizedDate,
                        request.Notes, TransactionType.Income, 1, parentTransactionId: existing.Id);
                    await txRepository.AddAsync(childTx, ct);
                    var childP = childTx.CreateParticipant(p.PersonId, p.ContributedAmount, 0, false, p.DestinationAccountId);
                    await txRepository.AddParticipantAsync(childP, ct);
                    participantDelta += p.ContributedAmount;
                }
            }
        }

        account.ApplyTransaction(tx.Amount, tx.ExchangeRate, tx.Installments, tx.Type);
        if (participantDelta != 0)
            account.Balance = Money.FromAny(account.Balance.Amount + participantDelta, account.Balance.Currency);
        await accountRepository.UpdateBalanceAsync(account.UserId, account.Id, account.Balance.Amount, ct);

        return Result.Success;
    }

    private async Task RevertChildrenAsync(Account account, Guid parentId, CancellationToken ct)
    {
        var children = await txRepository.GetChildTransactionsForDeleteAsync(parentId, ct);
        foreach (var child in children.Where(c => c.Type == TransactionType.Income))
            account.RevertTransaction(ToMoney(child), child.ExchangeRate, child.Installments, child.Type);
    }

    private static Money ToMoney(TransactionInfo t)
        => Money.FromPositive(t.Amount, CurrencyCode.From(t.Currency));

    private static Money ToMoney(TransactionForUpdate t)
        => Money.FromPositive(t.Amount, CurrencyCode.From(t.Currency));
}
