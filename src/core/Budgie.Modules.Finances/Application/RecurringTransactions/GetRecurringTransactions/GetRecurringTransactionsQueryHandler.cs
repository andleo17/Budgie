using Budgie.Modules.Finances.Application.RecurringTransactions.Shared;
using Budgie.Modules.Finances.Domain.RecurringTransactions;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.RecurringTransactions.GetRecurringTransactions;

internal sealed class GetRecurringTransactionsQueryHandler(
    IRecurringTransactionRepository recurringTransactionRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetRecurringTransactionsQuery, ErrorOr<List<RecurringTransactionResponse>>>
{
    public async ValueTask<ErrorOr<List<RecurringTransactionResponse>>> Handle(GetRecurringTransactionsQuery request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();

        var templates = await recurringTransactionRepository.GetByUserIdAsync(
            userId, request.Applicability, request.IsActive, ct);

        return templates.Select(MapResponse).ToList();
    }

    private static RecurringTransactionResponse MapResponse(RecurringTransaction r) => new(
        r.Id, r.Name.Value, r.AccountId, r.CategoryId, r.Amount.Amount, r.Amount.Currency.Code,
        r.ExchangeRate, r.Type, r.Notes, r.Installments, r.IsInterestFree,
        r.Applicability, r.Frequency, r.CustomIntervalDays, r.EndType,
        r.EndAfterCount, r.EndDate, r.StartDate, r.NextDueDate,
        r.TimesApplied, r.LastAppliedDate, r.IsActive,
        r.Participants.Select(p => new RecurringTransactionParticipantDto(
            p.PersonId, p.ContributedAmount.Amount, p.RemainingAmount.Amount,
            p.IsTrackingOnly, p.DestinationAccountId)).ToList());
}
