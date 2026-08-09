using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.RecurringTransactions.UpdateRecurringTransaction;

internal sealed class UpdateRecurringTransactionCommandHandler(
    FinancesDbContext dbContext,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<UpdateRecurringTransactionCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(UpdateRecurringTransactionCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserProvider.GetUserId();

        var template = await dbContext.RecurringTransactions
            .Include(r => r.Participants)
            .FirstOrDefaultAsync(r => r.Id == request.Id && r.UserId == userId, cancellationToken);

        if (template is null)
            return Error.NotFound("RecurringTransaction.NotFound", "Template not found.");

        template.Update(
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

        // Replace participants
        var oldParticipants = template.ClearParticipants();
        dbContext.RecurringTransactionParticipants.RemoveRange(oldParticipants);

        if (request.Participants is not null)
        {
            foreach (var p in request.Participants)
            {
                template.CreateParticipant(p.PersonId, p.ContributedAmount, p.RemainingAmount, p.IsTrackingOnly, p.DestinationAccountId);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success;
    }
}