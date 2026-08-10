using System.Text.Json;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Modules.Finances.Infrastructure.Logging;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.Transactions.DeleteTransaction;

internal sealed class DeleteTransactionCommandHandler(
    FinancesDbContext dbContext,
    ILogger<DeleteTransactionCommandHandler> logger)
    : IRequestHandler<DeleteTransactionCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(DeleteTransactionCommand request, CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Transactions
            .Include(t => t.Account)
            .Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (transaction is null) return Error.NotFound("Transaction.NotFound", "Transaction not found.");

        if (transaction.IsInitialBalance)
            return Error.Validation("Transaction.CannotDeleteInitialBalance", "Initial balance transaction cannot be deleted.");

        // Restore participants when deleting a REPAYMENT that was created by BulkRepay
        if (transaction.Type == TransactionType.Repayment)
        {
            var metaSource = transaction.RepaidParticipants ?? transaction.Notes;
            if (metaSource is not null)
                await RestoreRepaidParticipants(metaSource, cancellationToken);
        }

        // When deleting a child transaction, handle account impact and remove parent participant
        if (transaction.ParentTransactionId.HasValue)
        {
            // Revert account impact for child INCOMEs (actual cash movement)
            if (transaction.Type == TransactionType.Income)
                transaction.Account.RevertTransaction(
                    transaction.Amount,
                    transaction.ExchangeRate,
                    transaction.Installments,
                    transaction.Type);

            var childParticipant = transaction.Participants.FirstOrDefault();
            if (childParticipant is not null)
            {
                var parentParticipant = await dbContext.TransactionParticipants
                    .FirstOrDefaultAsync(tp =>
                        tp.TransactionId == transaction.ParentTransactionId.Value &&
                        tp.PersonId == childParticipant.PersonId,
                        cancellationToken);

                if (parentParticipant is not null)
                    dbContext.TransactionParticipants.Remove(parentParticipant);
            }
        }

        // Delete child transactions when deleting a parent
        if (transaction.ParentTransactionId is null)
        {
            var children = await dbContext.Transactions
                .Where(t => t.ParentTransactionId == transaction.Id)
                .ToListAsync(cancellationToken);

            // Revert child INCOMEs (they had account impact)
            foreach (var child in children.Where(c => c.Type == TransactionType.Income))
            {
                transaction.Account.RevertTransaction(
                    child.Amount, child.ExchangeRate,
                    child.Installments, child.Type);
            }

            dbContext.Transactions.RemoveRange(children);

            // Revert account balance for the parent
            transaction.Account.RevertTransaction(
                transaction.Amount,
                transaction.ExchangeRate,
                transaction.Installments,
                transaction.Type);
        }

        dbContext.Transactions.Remove(transaction);
        await dbContext.SaveChangesAsync(cancellationToken);

        LogMessages.TransactionDeleted(logger, request.Id);

        return Result.Success;
    }

    private static JsonElement? TryExtractMetadata(string notes)
    {
        // Try parsing the entire string as JSON first
        if (JsonSerializer.Deserialize<JsonElement>(notes) is { } meta && meta.TryGetProperty("repaid", out _))
            return meta;

        // User notes may be prepended before the JSON metadata; find the last '{'
        var bracePos = notes.LastIndexOf('{');
        if (bracePos >= 0)
        {
            var candidate = notes[bracePos..];
            if (JsonSerializer.Deserialize<JsonElement>(candidate) is { } extracted &&
                extracted.TryGetProperty("repaid", out _))
                return extracted;
        }

        return null;
    }

    private async Task RestoreRepaidParticipants(string notes, CancellationToken cancellationToken)
    {
        var meta = TryExtractMetadata(notes);
        if (meta is null) return;

        var repaid = meta.Value.GetProperty("repaid");

        foreach (var entry in repaid.EnumerateArray())
        {
            var pid = entry.GetProperty("p").GetString();
            var amount = entry.GetProperty("a").GetDecimal();

            if (pid is null || amount <= 0) continue;

            var participant = await dbContext.TransactionParticipants.FindAsync([Guid.Parse(pid)], cancellationToken);
            if (participant is null) continue;

            participant.RestoreRemaining(amount);

            // Also restore parent expense participant (same person)
            var childTx = await dbContext.Transactions
                .Include(t => t.Participants)
                .FirstOrDefaultAsync(t => t.Id == participant.TransactionId, cancellationToken);

            if (childTx?.ParentTransactionId is not null)
            {
                var parentParticipant = await dbContext.TransactionParticipants
                    .FirstOrDefaultAsync(tp =>
                        tp.TransactionId == childTx.ParentTransactionId.Value &&
                        tp.PersonId == participant.PersonId, cancellationToken);

                parentParticipant?.RestoreRemaining(amount);
            }
        }
    }
}
