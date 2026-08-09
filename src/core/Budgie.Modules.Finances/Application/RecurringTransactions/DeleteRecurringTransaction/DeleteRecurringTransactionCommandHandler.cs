using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.RecurringTransactions.DeleteRecurringTransaction;

internal sealed class DeleteRecurringTransactionCommandHandler(
    FinancesDbContext dbContext,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<DeleteRecurringTransactionCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(DeleteRecurringTransactionCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserProvider.GetUserId();

        var template = await dbContext.RecurringTransactions
            .FirstOrDefaultAsync(r => r.Id == request.Id && r.UserId == userId, cancellationToken);

        if (template is null)
            return Error.NotFound("RecurringTransaction.NotFound", "Template not found.");

        dbContext.RecurringTransactions.Remove(template);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success;
    }
}