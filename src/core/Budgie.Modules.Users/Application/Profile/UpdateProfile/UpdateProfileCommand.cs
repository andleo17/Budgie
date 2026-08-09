using ErrorOr;
using Mediator;

namespace Budgie.Modules.Users.Application.Profile.UpdateProfile;

public record UpdateProfileCommand(string Name) : IRequest<ErrorOr<Success>>;
