using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Modules.Finances.Infrastructure.Logging;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.Transactions.UpdateTransaction;

internal sealed class UpdateTransactionCommandHandler(
    FinancesDbContext dbContext,
    ILogger<UpdateTransactionCommandHandler> logger)
    : IRequestHandler<UpdateTransactionCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(UpdateTransactionCommand request, CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Transactions
            .Include(t => t.Account)
            .Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (transaction is null) return Error.NotFound("Transaction.NotFound", "Transaction not found.");

        transaction.Account.RevertTransaction(
            transaction.Amount,
            transaction.ExchangeRate,
            transaction.Installments,
            transaction.Type);

        var normalizedDate = request.Date;

        if (transaction.IsInitialBalance && normalizedDate != transaction.Date)
        {
            return Error.Validation("Transaction.InitialBalanceDateLocked", "Initial balance transaction date cannot be modified.");
        }

        if (normalizedDate < transaction.Account.CreatedAt.Date)
        {
            return Error.Validation("Transaction.DateBeforeAccountCreation", "Transaction date cannot be before the account was created.");
        }

        transaction.Update(
            request.CategoryId,
            request.Amount,
            request.Currency,
            request.ExchangeRate,
            normalizedDate,
            request.Notes,
            request.Type,
            request.Installments,
            request.ParentTransactionId);

        transaction.Account.ApplyTransaction(
            Money.FromPositive(request.Amount, transaction.Amount.Currency),
            transaction.ExchangeRate,
            transaction.Installments,
            transaction.Type);

        // Sync participants
        if (request.Participants is not null)
        {
            if (request.Participants.Count(p => p.RemainingAmount == 0 && !p.IsTrackingOnly && p.DestinationAccountId.HasValue) > 1)
                return Error.Validation("Transaction.TooManyRepaid", "Only one participant can be repaid per transaction.");

            // Remove old child transactions
            var oldChildren = await dbContext.Transactions
                .Where(t => t.ParentTransactionId == request.Id)
                .ToListAsync(cancellationToken);

            // Revert child INCOMEs (they had account impact)
            foreach (var child in oldChildren.Where(c => c.Type == TransactionType.Income))
            {
                transaction.Account.RevertTransaction(
                    child.Amount, child.ExchangeRate,
                    child.Installments, child.Type);
            }

            dbContext.Transactions.RemoveRange(oldChildren);

            // Clear old participants through DbSet
            var oldParticipants = transaction.ClearParticipants();
            dbContext.TransactionParticipants.RemoveRange(oldParticipants);

            // Add new participants explicitly through DbSet
            foreach (var p in request.Participants)
            {
                var participant = transaction.CreateParticipant(p.PersonId, p.ContributedAmount, p.RemainingAmount, p.IsTrackingOnly, p.DestinationAccountId);
                dbContext.TransactionParticipants.Add(participant);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            // Create child transactions for participants
            foreach (var p in request.Participants)
            {
                if (p.IsTrackingOnly) continue;

                if (p.RemainingAmount > 0 && !p.IsTrackingOnly && request.Type != TransactionType.Loan)
                {
                    var childTx = Transaction.Create(
                        transaction.UserId,
                        transaction.AccountId,
                        null,
                        p.RemainingAmount,
                        transaction.Amount.Currency,
                        transaction.ExchangeRate,
                        normalizedDate,
                        request.Notes,
                        TransactionType.Loan,
                        1,
                        parentTransactionId: transaction.Id);

                    childTx.CreateParticipant(p.PersonId, p.RemainingAmount, p.RemainingAmount, false, null);
                    dbContext.Transactions.Add(childTx);
                }
                else if (p.RemainingAmount == 0 && p.DestinationAccountId.HasValue)
                {
                    var childTx = Transaction.Create(
                        transaction.UserId,
                        transaction.AccountId,
                        null,
                        p.ContributedAmount,
                        transaction.Amount.Currency,
                        transaction.ExchangeRate,
                        normalizedDate,
                        request.Notes,
                        TransactionType.Income,
                        1,
                        parentTransactionId: transaction.Id);

                    childTx.CreateParticipant(p.PersonId, p.ContributedAmount, 0, false, p.DestinationAccountId);
                    dbContext.Transactions.Add(childTx);

                    transaction.Account.ApplyTransaction(Money.FromPositive(p.ContributedAmount, transaction.Amount.Currency), transaction.ExchangeRate, 1, TransactionType.Income);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        LogMessages.TransactionUpdated(logger, request.Id);

        return Result.Success;
    }
}
