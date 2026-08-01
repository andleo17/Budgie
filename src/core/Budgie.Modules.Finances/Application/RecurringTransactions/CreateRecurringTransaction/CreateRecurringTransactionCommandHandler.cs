using Budgie.Modules.Finances.Domain.RecurringTransactions;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.RecurringTransactions.CreateRecurringTransaction;

internal sealed class CreateRecurringTransactionCommandHandler(
    FinancesDbContext dbContext,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<CreateRecurringTransactionCommand, ErrorOr<Guid>>
{
    public async Task<ErrorOr<Guid>> Handle(CreateRecurringTransactionCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserProvider.GetUserId();

        var account = await dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == request.AccountId, cancellationToken);
        if (account is null)
            return Error.NotFound("Account.NotFound", "Account not found.");

        if (request.CategoryId.HasValue)
        {
            var category = await dbContext.Categories.FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);
            if (category is null)
                return Error.NotFound("Category.NotFound", "Category not found.");
        }

        var template = RecurringTransaction.Create(
            userId,
            request.Name,
            request.AccountId,
            request.CategoryId,
            request.Amount,
            request.Currency,
            request.ExchangeRate,
            request.Type,
            request.Notes,
            request.Installments,
            request.IsInterestFree,
            request.Applicability,
            request.Frequency,
            request.CustomIntervalDays,
            request.EndType,
            request.EndAfterCount,
            request.EndDate,
            request.StartDate);

        if (request.Participants is not null)
        {
            foreach (var p in request.Participants)
            {
                template.CreateParticipant(p.PersonId, p.ContributedAmount, p.RemainingAmount, p.IsTrackingOnly, p.DestinationAccountId);
            }
        }

        dbContext.RecurringTransactions.Add(template);
        await dbContext.SaveChangesAsync(cancellationToken);

        return template.Id;
    }
}