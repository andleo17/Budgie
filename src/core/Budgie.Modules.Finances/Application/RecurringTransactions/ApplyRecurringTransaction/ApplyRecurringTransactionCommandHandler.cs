using Budgie.Modules.Finances.Domain.RecurringTransactions;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.RecurringTransactions.ApplyRecurringTransaction;

internal sealed class ApplyRecurringTransactionCommandHandler(
    FinancesDbContext dbContext,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<ApplyRecurringTransactionCommand, ErrorOr<Guid>>
{
    public async Task<ErrorOr<Guid>> Handle(ApplyRecurringTransactionCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserProvider.GetUserId();

        var template = await dbContext.RecurringTransactions
            .Include(r => r.Participants)
            .FirstOrDefaultAsync(r => r.Id == request.Id && r.UserId == userId, cancellationToken);

        if (template is null)
            return Error.NotFound("RecurringTransaction.NotFound", "Template not found.");

        var account = await dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == template.AccountId, cancellationToken);
        if (account is null)
            return Error.NotFound("Account.NotFound", "Account not found.");

        var tx = Transaction.Create(
            userId,
            template.AccountId,
            template.CategoryId,
            template.Amount.Amount,
            template.Amount.Currency,
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
        dbContext.Transactions.Add(tx);

        template.Apply();

        await dbContext.SaveChangesAsync(cancellationToken);

        return tx.Id;
    }
}