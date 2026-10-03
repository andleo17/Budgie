using ErrorOr;
using Mediator;

namespace Budgie.Modules.EmailImport.Application.Inbox.ApproveBatch;

/// <summary>
/// Approves every Pending row whose confidence is at least <see cref="MinConfidence"/>, up
/// to <see cref="Take"/> rows, highest confidence first (spec §8.1 "Aprobar los de alta
/// confianza"). The batch runs the same approval path as the single-row approve.
/// </summary>
/// <param name="MinConfidence">Inclusive confidence threshold in [0, 1].</param>
/// <param name="Take">Maximum rows to approve in this batch.</param>
public record ApproveBatchCommand(
    decimal MinConfidence,
    int Take = 100) : IRequest<ErrorOr<ApproveBatchResponse>>;

/// <summary>
/// Outcome of a batch approval. One bad row never aborts the batch: it is counted in
/// <see cref="Failed"/> and its description collected in <see cref="Failures"/> while the
/// remaining rows keep being approved (spec §8.1).
/// </summary>
public record ApproveBatchResponse(
    int Approved,
    int Failed,
    IReadOnlyList<string> Failures);
