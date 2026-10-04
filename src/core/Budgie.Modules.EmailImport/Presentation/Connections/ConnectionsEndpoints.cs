using Budgie.Modules.EmailImport.Application.Connections.DisconnectEmail;
using Budgie.Modules.EmailImport.Application.Connections.GetConnectionStatus;
using Budgie.Modules.EmailImport.Application.Connections.UpdateThreshold;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.EmailImport.Presentation.Connections;

/// <summary>
/// Read (connection status) and write (disconnect) endpoints of the connection surface.
/// Both live under the protected <c>/api/email-import</c> group — the OAuth callback is the
/// only route of the module outside it (see <see cref="CompleteConnectionEndpoint"/>).
/// </summary>
internal static class ConnectionsEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        // Metadata only: the response DTO carries no credential, encrypted or not (spec §11).
        // An empty list is a valid state ("nothing connected yet"), never NotFound.
        group.MapGet("/connections", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new GetConnectionStatusQuery());
            return result.Match(
                value => Results.Ok(value),
                errors => Results.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
        });

        group.MapDelete("/connections/{provider}", async (string provider, IMediator mediator) =>
        {
            if (!GetAuthorizationUriEndpoint.TryParseProvider(provider, out var parsedProvider))
                return GetAuthorizationUriEndpoint.UnknownProvider(provider);

            var result = await mediator.Send(new DisconnectEmailCommand(parsedProvider));
            return result.Match(
                _ => Results.NoContent(),
                errors => Results.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
        });

        // The auto-commit threshold used to be a column only — no route could change it, so the
        // settings slider was read-only. The domain has always validated the value
        // (ChangeAutoCommitThreshold → [0, 1]); the handler turns that rejection into a 400
        // instead of a 500.
        group.MapPut("/connections/{provider}/threshold", async (
            string provider, UpdateAutoCommitThresholdRequest req, IMediator mediator) =>
        {
            if (!GetAuthorizationUriEndpoint.TryParseProvider(provider, out var parsedProvider))
                return GetAuthorizationUriEndpoint.UnknownProvider(provider);

            var result = await mediator.Send(
                new UpdateAutoCommitThresholdCommand(parsedProvider, req.Threshold));
            return result.Match(
                _ => Results.NoContent(),
                errors => Results.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
        });
    }
}

/// <summary>
/// Body of <c>PUT /api/email-import/connections/{provider}/threshold</c>. A single value: the
/// minimum confidence a parsed email needs to be committed without review.
/// </summary>
/// <param name="Threshold">New minimum confidence, in [0, 1].</param>
public record UpdateAutoCommitThresholdRequest(decimal Threshold);
