using ErrorOr;
using Mediator;

namespace Budgie.Modules.Users.Application.Profile.GetProfile;

public record GetProfileQuery() : IRequest<ErrorOr<ProfileResponse>>;
