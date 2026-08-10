using Budgie.Modules.Users.Application;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Users.Application.Profile.UpdateProfile;

internal sealed class UpdateProfileCommandHandler(
    IUserRepository userRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<UpdateProfileCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(UpdateProfileCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var user = await userRepository.GetByIdAsync(userId, ct);
        if (user is null) return Error.NotFound("User.NotFound", "User not found.");

        user.Update(request.Name);
        await userRepository.UpdateAsync(user, ct);
        return Result.Success;
    }
}
