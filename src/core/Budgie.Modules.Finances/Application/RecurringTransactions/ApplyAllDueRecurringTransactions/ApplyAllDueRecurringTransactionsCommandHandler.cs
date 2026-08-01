using Budgie.Modules.Finances.Domain.RecurringTransactions;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.RecurringTransactions.ApplyAllDueRecurringTransactions;

internal sealed class ApplyAllDueRecurringTransactionsCommandHandler(
    FinancesDbContext dbContext,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<ApplyAllDueRecurringTransactionsCommand, ErrorOr<List<Guid>>>
{
    public async Task<ErrorOr<List<Guid>>> Handle(ApplyAllDueRecurringTransactionsCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserProvider.GetUserId();
        var now = DateTime.UtcNow;

        var due = await dbContext.RecurringTransactions
            .Include(r => r.Participants)
            .Where(r => r.UserId == userId
                && r.IsActive
                && r.Applicability == RecurringApplicability.Automatic
                && r.NextDueDate != null
                && r.NextDueDate <= now)
            .ToListAsync(cancellationToken);

        var createdIds = new List<Guid>();

        foreach (var template in due)
        {
            var account = await dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == template.AccountId, cancellationToken);
            if (account is null) continue;

            var tx = Transaction.Create(
                userId,
                template.AccountId,
                template.CategoryId,
                template.Amount.Amount,
                template.Amount.Currency,
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
            dbContext.Transactions.Add(tx);
            template.Apply();
            createdIds.Add(tx.Id);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return createdIds;
    }
}