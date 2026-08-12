using Budgie.Modules.Finances.Application;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.RecurringTransactions.DeleteRecurringTransaction;

internal sealed class DeleteRecurringTransactionCommandHandler(
    IRecurringTransactionRepository recurringTransactionRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<DeleteRecurringTransactionCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(DeleteRecurringTransactionCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();

        var template = await recurringTransactionRepository.GetByIdAsync(request.Id, userId, ct);
        if (template is null)
            return Error.NotFound("RecurringTransaction.NotFound", "Template not found.");

        await recurringTransactionRepository.DeleteAsync(request.Id, userId, ct);
        return Result.Success;
    }
}
