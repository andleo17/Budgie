using Budgie.Modules.Users.Application;
using Budgie.Modules.Users.Domain;
using Budgie.Modules.Users.Infrastructure.Logging;
using ErrorOr;
using Mediator;
using BC = BCrypt.Net.BCrypt;

namespace Budgie.Modules.Users.Application.Register;

internal sealed class RegisterUserCommandHandler(
    IUserRepository userRepository,
    IUserSettingsRepository settingsRepository,
    ILogger<RegisterUserCommandHandler> logger)
    : IRequestHandler<RegisterUserCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(RegisterUserCommand request, CancellationToken ct)
    {
        if (await userRepository.EmailExistsAsync(request.Email, ct))
            return Error.Conflict("User.EmailAlreadyExists", "Email is already in use.");

        var user = User.Create(request.Email, BC.HashPassword(request.Password), request.Name);
        var settings = UserSettings.Create(user.Id);

        await userRepository.AddAsync(user, ct);
        await settingsRepository.AddAsync(settings, ct);

        LogMessages.UserRegistered(logger, request.Email);
        return user.Id;
    }
}
