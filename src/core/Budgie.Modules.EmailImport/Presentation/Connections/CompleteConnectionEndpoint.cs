using Budgie.Modules.EmailImport.Application.Connections.CompleteConnection;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.EmailImport.Presentation.Connections;

/// <summary>
/// OAuth callback — <c>GET /api/email-import/connections/{provider}/callback?code&amp;state</c>.
///
/// <para><b>THE ONLY ENDPOINT OF THE MODULE WITHOUT <c>RequireAuthorization</c>, AND THAT IS
/// DELIBERATE (spec §6).</b> The provider redirects the user's browser here, and in that
/// navigation Budgie's JWT does NOT travel: the token lives in localStorage, not in a cookie
/// that would ride along on the redirect. The callback therefore cannot authenticate with the
/// JWT — it authenticates with the signed <c>state</c> alone (HMAC over userId+provider+
/// redirectUri, single-use nonce burned on consume, 10-minute expiry — OAuthStateService).
/// </para>
///
/// <para><b>Consequence — read before "fixing" this:</b> this endpoint must NOT inject
/// <c>ICurrentUserProvider</c>. The <c>userId</c> comes exclusively from the consumed state;
/// that is what binds the connection to the user who STARTED the flow, no matter whose
/// browser (or attacker with a stolen code) completes it. A valid state issued for user A
/// stays bound to user A — a caller-supplied identity could only weaken that.</para>
///
/// <para>The route's <c>{provider}</c> segment exists solely to mirror the redirect URI the
/// user registered in the provider console. The authoritative provider is the one sealed
/// inside the state; the segment is intentionally not even bound here.</para>
///
/// <para>The authorization <c>code</c> is a single-use credential: it is never logged.</para>
/// </summary>
/// <remarks>
/// Mapped on <c>app</c> directly (outside the protected group) in
/// <c>EmailImportModule.MapEmailImportEndpoints</c>, so no <c>RequireAuthorization</c> is
/// inherited. On success the user is sent back to <c>/settings?emailImport=connected</c>; on
/// failure, to <c>/email-import?emailImport=error&amp;reason=&lt;error code&gt;</c> — the bare
/// ErrorOr code (e.g. <c>EmailImport.InvalidState</c>), never internal details.
/// </remarks>
public static class CompleteConnectionEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/email-import/connections/{provider}/callback", async (
            string? code,
            string? state,
            IMediator mediator) =>
        {
            // Missing code/state (user landed here directly, or the provider stripped the
            // query): same InvalidState treatment as a bad state — no code, no exchange.
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
                return ErrorRedirect("EmailImport.InvalidState");

            var result = await mediator.Send(new CompleteConnectionCommand(state!, code!));

            return result.Match(
                _ => Results.Redirect("/settings?emailImport=connected"),
                errors => ErrorRedirect(errors.First().Code));
        });
    }

    /// <summary>
    /// Redirect back to the frontend carrying only the machine-readable error code.
    /// <see cref="Uri.EscapeDataString"/> keeps the query well-formed regardless of the code.
    /// </summary>
    private static IResult ErrorRedirect(string reasonCode) =>
        Results.Redirect($"/email-import?emailImport=error&reason={Uri.EscapeDataString(reasonCode)}");
}
