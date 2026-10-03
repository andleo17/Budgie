using Budgie.Modules.EmailImport.Application.Inbox.ApprovePending;
using Budgie.Modules.EmailImport.Domain.Staging;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.EmailImport.Application.Inbox.DismissPending;

/// <inheritdoc cref="DismissPendingCommand"/>
internal sealed class DismissPendingCommandHandler(
    IPendingEmailTransactionRepository pendingRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<DismissPendingCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(DismissPendingCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();

        var pending = await pendingRepository.GetByIdAsync(userId, request.Id, ct);
        if (pending is null || pending.Status != PendingEmailStatus.Pending)
            return PendingApprovalHelper.PendingNotFound();

        // NOT a delete (spec §3.5, §8.1): MarkDismissed transitions the row to Dismissed
        // and keeps it, so its MessageId keeps blocking re-import of the same email and
        // the dismissal stays auditable.
        await pendingRepository.MarkDismissedAsync(userId, pending.Id, DateTime.UtcNow, ct);

        return Result.Success;
    }
}
