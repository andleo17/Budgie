using Budgie.Modules.Users.Application;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Users.Application.Profile.GetProfile;

internal sealed class GetProfileQueryHandler(
    IUserRepository userRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetProfileQuery, ErrorOr<ProfileResponse>>
{
    public async ValueTask<ErrorOr<ProfileResponse>> Handle(GetProfileQuery request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var user = await userRepository.GetByIdAsync(userId, ct);
        return user is not null
            ? new ProfileResponse(user.Id, user.Email, user.Name)
            : Error.NotFound("User.NotFound", "User not found.");
    }
}
