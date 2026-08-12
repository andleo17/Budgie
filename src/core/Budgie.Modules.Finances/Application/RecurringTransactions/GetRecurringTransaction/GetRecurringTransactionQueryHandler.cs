using Budgie.Modules.Finances.Application.RecurringTransactions.Shared;
using Budgie.Modules.Finances.Domain.RecurringTransactions;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.RecurringTransactions.GetRecurringTransaction;

internal sealed class GetRecurringTransactionQueryHandler(
    IRecurringTransactionRepository recurringTransactionRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetRecurringTransactionQuery, ErrorOr<RecurringTransactionResponse>>
{
    public async ValueTask<ErrorOr<RecurringTransactionResponse>> Handle(GetRecurringTransactionQuery request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();

        var template = await recurringTransactionRepository.GetByIdAsync(request.Id, userId, ct);
        if (template is null)
            return Error.NotFound("RecurringTransaction.NotFound", "Template not found.");

        return new RecurringTransactionResponse(
            template.Id, template.Name.Value, template.AccountId, template.CategoryId,
            template.Amount.Amount, template.Amount.Currency.Code, template.ExchangeRate, template.Type,
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
