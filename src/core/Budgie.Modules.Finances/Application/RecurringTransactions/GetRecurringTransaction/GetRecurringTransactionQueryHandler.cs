using Budgie.Modules.Finances.Application.RecurringTransactions.Shared;
using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.RecurringTransactions.GetRecurringTransaction;

internal sealed class GetRecurringTransactionQueryHandler(
    FinancesDbContext dbContext,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetRecurringTransactionQuery, ErrorOr<RecurringTransactionResponse>>
{
    public async Task<ErrorOr<RecurringTransactionResponse>> Handle(GetRecurringTransactionQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserProvider.GetUserId();

        var template = await dbContext.RecurringTransactions
            .Include(r => r.Participants)
            .FirstOrDefaultAsync(r => r.Id == request.Id && r.UserId == userId, cancellationToken);

        if (template is null)
            return Error.NotFound("RecurringTransaction.NotFound", "Template not found.");

        return new RecurringTransactionResponse(
            template.Id, template.Name, template.AccountId, template.CategoryId,
            template.Amount.Amount, template.Amount.Currency, template.ExchangeRate, template.Type,
            template.Notes, template.Installments, template.IsInterestFree,
            template.Applicability, template.Frequency, template.CustomIntervalDays,
            template.EndType, template.EndAfterCount, template.EndDate, template.StartDate,
            template.NextDueDate, template.TimesApplied, template.LastAppliedDate,
            template.IsActive,
            template.Participants.Select(p => new RecurringTransactionParticipantDto(
                p.PersonId, p.ContributedAmount.Amount, p.RemainingAmount.Amount,
                p.IsTrackingOnly, p.DestinationAccountId)).ToList());
    }
}