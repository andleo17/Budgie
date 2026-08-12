using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Domain.RecurringTransactions;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.RecurringTransactions.CreateRecurringTransaction;

internal sealed class CreateRecurringTransactionCommandHandler(
    IAccountRepository accountRepository,
    ICategoryRepository categoryRepository,
    IRecurringTransactionRepository recurringTransactionRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<CreateRecurringTransactionCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(CreateRecurringTransactionCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();

        var account = await accountRepository.GetByIdAsync(request.AccountId, userId, ct);
        if (account is null)
            return Error.NotFound("Account.NotFound", "Account not found.");

        if (request.CategoryId.HasValue)
        {
            var category = await categoryRepository.GetByIdAsync(request.CategoryId.Value, userId, ct);
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

        await recurringTransactionRepository.AddAsync(template, ct);
        if (template.Participants.Count > 0)
            await recurringTransactionRepository.AddParticipantsAsync(template, ct);

        return template.Id;
    }
}
