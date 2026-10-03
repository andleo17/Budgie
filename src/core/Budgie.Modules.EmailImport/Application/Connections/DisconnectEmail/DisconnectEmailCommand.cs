using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.EmailImport.Application.Connections.DisconnectEmail;

/// <summary>
/// Disconnects the mailbox connected for one provider. The <c>ON DELETE CASCADE</c> of the
/// schema removes the connection's rules and staging rows with it.
/// </summary>
/// <param name="Provider">The provider to disconnect.</param>
public record DisconnectEmailCommand(EmailProvider Provider) : IRequest<ErrorOr<Success>>;
