using Budgie.Modules.Users.Application.Settings;
using Budgie.Modules.Users.Application.Settings.GetSettings;
using Budgie.Modules.Users.Application.Settings.UpdateSettings;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Users.Presentation.Settings;

public static class SettingsEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("settings").WithTags("Settings");

        group.MapGet("", async (ISender sender) =>
        {
            var result = await sender.Send(new GetSettingsQuery());
            return result.Match(
                settings => Results.Ok(settings),
                errors => Results.BadRequest(errors));
        })
        .Produces<UserSettingsResponse>(200);

        group.MapPut("", async (UpdateSettingsCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.Match(
                _ => Results.NoContent(),
                errors => Results.BadRequest(errors));
        })
        .Produces(204);
 group.MapPost("setup", async (UpdateSettingsCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.Match(
                _ => Results.NoContent(),
                errors => Results.BadRequest(errors));
        })
        .Produces(204);
    }
}
