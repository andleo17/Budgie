using System.Text.Json;
using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Modules.Finances.Infrastructure.Logging;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.Transactions.BulkRepay;

internal sealed class BulkRepayCommandHandler(
    FinancesDbContext dbContext,
    ICurrentUserProvider currentUserProvider,
    ILogger<BulkRepayCommandHandler> logger)
    : IRequestHandler<BulkRepayCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(BulkRepayCommand request, CancellationToken cancellationToken)
    {
        var account = await dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == request.AccountId, cancellationToken);
        if (account is null)
            return Error.NotFound("Account.NotFound", "Account not found.");

        var normalizedDate = request.Date;

        // Reduce remaining amounts on each selected debt and track what was repaid
        var repaidEntries = new List<object>();
        foreach (var debt in request.Debts)
        {
            var participant = await dbContext.TransactionParticipants
                .Include(tp => tp.Transaction)
                .FirstOrDefaultAsync(tp => tp.Id == debt.ParticipantId, cancellationToken);

            if (participant is null) continue;

            participant.ReduceRemaining(debt.Amount);
            repaidEntries.Add(new { p = debt.ParticipantId.ToString(), a = debt.Amount });

            // Also reduce the parent transaction's participant (same person)
            if (participant.Transaction.ParentTransactionId.HasValue)
            {
                var parentParticipant = await dbContext.TransactionParticipants
                    .FirstOrDefaultAsync(tp =>
                        tp.TransactionId == participant.Transaction.ParentTransactionId.Value &&
                        tp.PersonId == participant.PersonId &&
                        tp.RemainingAmount.Amount > 0,
                        cancellationToken);

                parentParticipant?.ReduceRemaining(debt.Amount);
            }
        }

        // Store repaid participant IDs in notes for later restoration on delete
        var notes = request.Notes;
        if (repaidEntries.Count > 0)
        {
            var meta = JsonSerializer.Serialize(new { repaid = repaidEntries });
            notes = notes is not null ? $"{notes}\n{meta}" : meta;
        }

        var repaymentWithMeta = Transaction.Create(
            currentUserProvider.GetUserId(),
            request.AccountId,
            SystemCategoryIds.LoanRepayment,
            request.Amount,
            request.Currency,
            request.ExchangeRate,
            normalizedDate,
            notes,
            TransactionType.Repayment);

        account.ApplyTransaction(Money.FromPositive(request.Amount, CurrencyCode.From(request.Currency)), request.ExchangeRate, 1, TransactionType.Repayment);

        dbContext.Transactions.Add(repaymentWithMeta);
        await dbContext.SaveChangesAsync(cancellationToken);

        LogMessages.LoanRepaid(logger, repaymentWithMeta.Id, request.Amount, request.Currency);

        return repaymentWithMeta.Id;
    }
}
