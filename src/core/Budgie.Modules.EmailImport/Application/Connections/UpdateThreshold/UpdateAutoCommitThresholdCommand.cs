using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.EmailImport.Application.Connections.UpdateThreshold;

/// <summary>
/// Changes the minimum confidence a mailbox requires to auto-commit a parsed email.
/// </summary>
/// <remarks>
/// The value was previously only reachable through the database: it is a column, it is a
/// parameter of <c>EmailConnection.Create</c>, and the domain has always validated
/// <c>ChangeAutoCommitThreshold</c> — but no route exposed it, so the settings slider had to
/// be read-only with a note saying so. A parameter that looks configurable and is not is worse
/// than not offering it.
/// </remarks>
/// <param name="Provider">The provider whose mailbox threshold changes.</param>
/// <param name="Threshold">New minimum confidence, in [0, 1].</param>
public record UpdateAutoCommitThresholdCommand(EmailProvider Provider, decimal Threshold)
    : IRequest<ErrorOr<Success>>;