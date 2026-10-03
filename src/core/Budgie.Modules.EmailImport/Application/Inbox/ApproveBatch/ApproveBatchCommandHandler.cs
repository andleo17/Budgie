using Budgie.Modules.EmailImport.Application.Inbox.Abstractions;
using Budgie.Modules.EmailImport.Application.Inbox.ApprovePending;
using Budgie.Modules.Finances.Application;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.EmailImport.Application.Inbox.ApproveBatch;

/// <inheritdoc cref="ApproveBatchCommand"/>
/// <remarks>
/// Approving in bulk goes through <see cref="PendingApprovalHelper"/> — the SAME code path
/// as the single-row approve, including both idempotency barriers (spec §3.5). One bad
/// row is counted and skipped; it never aborts the batch (spec §8.1).
/// </remarks>
internal sealed class ApproveBatchCommandHandler(
    IPendingEmailTransactionRepository pendingRepository,
    IEmailImportRuleRepository ruleRepository,
    IEmailConnectionRepository connectionRepository,
    IAccountRepository accountRepository,
    ICategoryRepository categoryRepository,
    ITransactionRepository transactionRepository,
    IMediator mediator,
    ICurrentUserProvider currentUserProvider,
    ICurrentUserProviderAccessor currentUserAccessor)
    : IRequestHandler<ApproveBatchCommand, ErrorOr<ApproveBatchResponse>>
{
    public async ValueTask<ErrorOr<ApproveBatchResponse>> Handle(
        ApproveBatchCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var take = Math.Max(1, request.Take);

        var rows = await pendingRepository.GetHighConfidenceAsync(
            userId, request.MinConfidence, take, ct);

        var context = new PendingApprovalContext(
            pendingRepository, ruleRepository, connectionRepository,
            accountRepository, categoryRepository, transactionRepository,
            mediator, currentUserAccessor);

        var approved = 0;
        var failed = 0;
        var failures = new List<string>();

        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();

            // All-null corrections: the batch approves rows exactly as staged. The helper
            // re-checks the Pending status, so a row flipped since the listing fails this
            // one row instead of duplicating its transaction.
            var result = await PendingApprovalHelper.ApproveAsync(
                userId, row, new ApprovePendingCommand(row.Id), context, ct);

            if (result.IsError)
            {
                // One bad row (its rule deleted leaving no account, a
                // BeforeControlStartDate rejection, a row already committed by the worker…)
                // never aborts the batch: it is counted and the loop continues (spec §8.1).
                failed++;
                failures.Add(result.Errors[0].Description);
                continue;
            }

            approved++;
        }

        return new ApproveBatchResponse(approved, failed, failures);
    }
}
