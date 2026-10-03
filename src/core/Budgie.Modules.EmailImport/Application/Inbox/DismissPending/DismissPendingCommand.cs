using ErrorOr;
using Mediator;

namespace Budgie.Modules.EmailImport.Application.Inbox.DismissPending;

/// <summary>
/// Discards one staged row. <b>Not a delete</b>: the row is kept so its MessageId keeps
/// blocking re-import of the same email (spec §3.5 first barrier) and so the dismissal
/// stays auditable.
/// </summary>
/// <param name="Id">Staging row id.</param>
public record DismissPendingCommand(Guid Id) : IRequest<ErrorOr<Success>>;
