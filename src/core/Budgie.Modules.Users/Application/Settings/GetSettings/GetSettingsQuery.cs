using ErrorOr;
using Mediator;

namespace Budgie.Modules.Users.Application.Settings.GetSettings;

public record GetSettingsQuery() : IRequest<ErrorOr<UserSettingsResponse>>;
