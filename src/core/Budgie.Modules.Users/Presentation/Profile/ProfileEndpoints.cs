using Budgie.Modules.Users.Application.Profile;
using Budgie.Modules.Users.Application.Profile.GetProfile;
using Budgie.Modules.Users.Application.Profile.UpdateProfile;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Users.Presentation.Profile;

public static class ProfileEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("profile").RequireAuthorization().WithTags("Profile");

        group.MapGet("", async (ISender sender) =>
        {
            var result = await sender.Send(new GetProfileQuery());
            return result.Match(
                profile => Results.Ok(profile),
                errors => Results.BadRequest(errors));
        })
        .Produces<ProfileResponse>(200);

        group.MapPut("", async (UpdateProfileCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.Match(
                _ => Results.NoContent(),
                errors => Results.BadRequest(errors));
        })
        .Produces(204);
    }
}
