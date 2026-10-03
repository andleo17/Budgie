using Budgie.Modules.EmailImport.Application.Inbox.SyncNow;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.EmailImport.Presentation.Inbox;

/// <summary>
/// On-demand synchronisation (<c>POST /api/email-import/sync</c>): runs one sync pass for
/// the current user's mailboxes and returns the tally.
/// </summary>
/// <remarks>
/// The handler answers <c>EmailImport.SyncAlreadyScheduled</c> (Conflict) while the
/// background worker owns the schedule (<c>EmailImport:Enabled = true</c>): two syncs
/// racing each other would only duplicate work over the same cursor. The endpoint just
/// relays that decision — the schedule flag is operator configuration, never a request
/// parameter.
/// </remarks>
public static class SyncNowEndpoint
{
    public static void MapEndpoint(RouteGroupBuilder group)
    {
        group.MapPost("/sync", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new SyncNowCommand());
            return result.Match(
                value => Results.Ok(value),
                errors => Results.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
        });
    }
}
