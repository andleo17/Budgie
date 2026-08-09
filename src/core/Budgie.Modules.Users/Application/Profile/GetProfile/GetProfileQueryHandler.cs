using Budgie.Modules.Users.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Users.Application.Profile.GetProfile;

internal sealed class GetProfileQueryHandler(
    UsersDbContext dbContext,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetProfileQuery, ErrorOr<ProfileResponse>>
{
    public async ValueTask<ErrorOr<ProfileResponse>> Handle(GetProfileQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserProvider.GetUserId();
        var user = await dbContext.Users
            .Where(u => u.Id == userId)
            .Select(u => new ProfileResponse(u.Id, u.Email, u.Name))
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            return Error.NotFound("User.NotFound", "User not found.");

        return user;
    }
}
