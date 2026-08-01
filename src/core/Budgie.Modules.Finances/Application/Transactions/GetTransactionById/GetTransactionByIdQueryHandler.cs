using Budgie.Modules.Finances.Application.Transactions.CreateTransaction;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using Budgie.Modules.Finances.Infrastructure.Database;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.Transactions.GetTransactionById;

internal sealed class GetTransactionByIdQueryHandler(FinancesDbContext dbContext)
    : IRequestHandler<GetTransactionByIdQuery, ErrorOr<TransactionResponse>>
{
    public async Task<ErrorOr<TransactionResponse>> Handle(GetTransactionByIdQuery request, CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Transactions
            .Include(t => t.Account)
            .Include(t => t.Category)
            .Include(t => t.Participants)
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (transaction is null)
            return Error.NotFound("Transaction.NotFound", "Transaction not found.");

        Guid? destId = null;
        string? destName = null;

        if (transaction.Type == TransactionType.Transfer && transaction.LinkedTransactionId.HasValue)
        {
            var dest = await dbContext.Transactions
                .Include(t => t.Account)
                .Where(t => t.LinkedTransactionId == transaction.LinkedTransactionId && t.Type != TransactionType.Transfer)
                .Select(t => new { t.AccountId, AccountName = t.Account.Name.Value })
                .FirstOrDefaultAsync(cancellationToken);

            if (dest is not null)
            {
                destId = dest.AccountId;
                destName = dest.AccountName;
            }
        }

        return new TransactionResponse(
            transaction.Id,
            transaction.AccountId,
            transaction.Account.Name.Value,
            transaction.CategoryId,
            transaction.Category?.Name?.Value,
            transaction.Amount.Amount,
            transaction.Amount.Currency,
            transaction.ExchangeRate,
            Guard.NormalizeDate(transaction.Date),
            transaction.Notes,
            transaction.Type,
            transaction.Installments,
            transaction.IsInitialBalance,
            null,
            transaction.LinkedTransactionId,
            transaction.ParentTransactionId,
            destId,
            destName,
            transaction.Participants.Select(p => new TransactionParticipantDto(
                p.PersonId,
                p.ContributedAmount.Amount,
                p.RemainingAmount.Amount,
                p.IsTrackingOnly,
                p.DestinationAccountId)).ToList(),
            InstallmentGroupId: transaction.InstallmentGroupId,
            InstallmentNumber: transaction.InstallmentNumber,
            TotalInstallments: transaction.TotalInstallments,
            InterestRate: transaction.InterestRate);
    }
}