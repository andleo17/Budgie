using ErrorOr;
using Mediator;

namespace Budgie.Modules.EmailImport.Application.Connections.CompleteConnection;

/// <summary>
/// Completes the OAuth flow from the provider's callback: validates the signed state,
/// exchanges the authorization code and persists the connection.
/// </summary>
/// <param name="State">The signed state echoed back by the provider on the callback.</param>
/// <param name="Code">The authorization code the provider attached to the callback.</param>
public record CompleteConnectionCommand(string State, string Code) : IRequest<ErrorOr<Guid>>;
