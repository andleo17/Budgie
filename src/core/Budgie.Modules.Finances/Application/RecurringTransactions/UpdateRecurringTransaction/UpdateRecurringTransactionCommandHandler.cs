using Budgie.Modules.Finances.Application;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.RecurringTransactions.UpdateRecurringTransaction;

internal sealed class UpdateRecurringTransactionCommandHandler(
    IRecurringTransactionRepository recurringTransactionRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<UpdateRecurringTransactionCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(UpdateRecurringTransactionCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();

        var template = await recurringTransactionRepository.GetByIdAsync(request.Id, userId, ct);
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

        template.ClearParticipants();
        if (request.Participants is not null)
        {
            foreach (var p in request.Participants)
            {
                template.CreateParticipant(p.PersonId, p.ContributedAmount, p.RemainingAmount, p.IsTrackingOnly, p.DestinationAccountId);
            }
        }

        await recurringTransactionRepository.UpdateAsync(template, ct);
        await recurringTransactionRepository.ReplaceParticipantsAsync(template, ct);

        return Result.Success;
    }
}
