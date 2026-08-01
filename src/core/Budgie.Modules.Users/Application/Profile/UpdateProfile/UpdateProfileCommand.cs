using ErrorOr;
using MediatR;

namespace Budgie.Modules.Users.Application.Profile.UpdateProfile;

public record UpdateProfileCommand(string Name) : IRequest<ErrorOr<Success>>;
