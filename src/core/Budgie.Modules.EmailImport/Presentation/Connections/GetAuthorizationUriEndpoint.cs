using Budgie.Modules.EmailImport.Application;
using Budgie.Modules.EmailImport.Application.Connections;
using Budgie.Modules.EmailImport.Application.Connections.GetAuthorizationUri;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.EmailImport.Presentation.Connections;

/// <summary>
/// Starts the OAuth flow for one provider: returns the absolute URI the user must visit.
/// </summary>
/// <remarks>
/// The provider travels as a query parameter (<c>?provider=gmail</c>) because spec §6
/// defines the route as <c>POST /connections/authorize</c> with no provider segment.
/// The <c>redirectUri</c> is derived HERE, from the request's scheme + host — Application
/// cannot read <c>HttpContext</c>, so the endpoint computes it and passes it to the
/// command. The provider segment is lowercased to mirror exactly what the user registers
/// in the provider console: <c>https://&lt;host&gt;/api/email-import/connections/gmail/callback</c>.
/// </remarks>
public static class GetAuthorizationUriEndpoint
{
    public static void MapEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/connections/authorize", async (
            string provider,
            HttpRequest request,
            IMediator mediator) =>
        {
            if (!TryParseProvider(provider, out var parsedProvider))
                return UnknownProvider(provider);

            var redirectUri =
                $"{request.Scheme}://{request.Host}/api/email-import/connections/" +
                $"{parsedProvider.ToString().ToLowerInvariant()}/callback";

            ErrorOr<EmailAuthorizationUri> result;
            try
            {
                result = await mediator.Send(
                    new GetAuthorizationUriCommand(parsedProvider, redirectUri));
            }
            catch (EmailProviderException ex)
            {
                // Building the URI can fail before any network call when the deployment has
                // no OAuth client configured (EmailProviderError.NotConfigured). That is an
                // operational problem, not a user error — but it must still be a controlled
                // response with a stable code, never an unhandled 500.
                return ex.Error == EmailProviderError.NotConfigured
                    ? Results.BadRequest(new Dictionary<string, string[]>
                    {
                        ["EmailImport.ProviderNotConfigured"] =
                            [$"The email provider '{parsedProvider}' is not configured on this server. Set its OAuth client id/secret (EmailImport:Providers:{parsedProvider.ToString().ToLowerInvariant()})."]
                    })
                    : Results.BadRequest(new Dictionary<string, string[]>
                    {
                        ["EmailImport.ProviderUnavailable"] =
                            [$"The email provider '{parsedProvider}' could not be reached ({ex.Error}). Try again later."]
                    });
            }

            return result.Match(
                value => Results.Ok(value),
                errors => Results.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
        });
    }

    /// <summary>Case-insensitive parse of the provider route/query value.</summary>
    internal static bool TryParseProvider(string? value, out EmailProvider provider) =>
        Enum.TryParse(value, ignoreCase: true, out provider) && Enum.IsDefined(provider);

    internal static IResult UnknownProvider(string? value) =>
        Results.BadRequest(new Dictionary<string, string[]>
        {
            ["EmailImport.UnknownProvider"] = [$"Unknown email provider '{value}'."]
        });
}
