using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.EmailImport.Application.Connections.GetAuthorizationUri;

/// <summary>
/// Starts the OAuth flow for one provider: produces the URI the user must visit.
/// </summary>
/// <param name="Provider">The provider to connect.</param>
/// <param name="redirectUri">
/// Where the provider must send the user back. Derived by the endpoint from the request
/// scheme + host — Application cannot read <c>HttpContext</c>, so it arrives here.
/// </param>
public record GetAuthorizationUriCommand(EmailProvider Provider, string RedirectUri)
    : IRequest<ErrorOr<EmailAuthorizationUri>>;
