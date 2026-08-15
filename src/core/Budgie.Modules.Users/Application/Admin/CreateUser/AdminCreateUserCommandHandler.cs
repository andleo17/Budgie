using Budgie.Modules.Users.Application;
using Budgie.Modules.Users.Domain;
using Budgie.Modules.Users.Domain.Events;
using Budgie.Modules.Users.Infrastructure.Logging;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;
using Microsoft.Extensions.Logging;
using System.Globalization;
using BC = BCrypt.Net.BCrypt;

namespace Budgie.Modules.Users.Application.Admin.CreateUser;

public record AdminCreateUserCommand(string Email, string Name, string InitialPassword, UserRole Role = UserRole.User) : IRequest<ErrorOr<Guid>>;

internal sealed class AdminCreateUserCommandHandler(
    IUserRepository userRepository,
    ICurrentUserProvider currentUserProvider,
    IMediator mediator,
    ILogger<AdminCreateUserCommandHandler> logger)
    : IRequestHandler<AdminCreateUserCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(AdminCreateUserCommand request, CancellationToken ct)
    {
        var currentUserId = currentUserProvider.GetUserId();
        var currentUser = await userRepository.GetByIdAsync(currentUserId, ct);

        if (currentUser is null || currentUser.Role != UserRole.Admin)
        {
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
                ? Error.Forbidden("Auth.AdminRequired", "Admin privileges required to provision users.")
                : Error.Forbidden("Auth.AdminRequired", "Se requieren privilegios de Administrador para aprovisionar usuarios.");
        }

        if (await userRepository.EmailExistsAsync(request.Email, ct))
        {
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
                ? Error.Conflict("User.EmailAlreadyExists", "Email is already in use.")
                : Error.Conflict("User.EmailAlreadyExists", "El correo electrónico ya está en uso.");
        }

        var user = User.Create(request.Email, BC.HashPassword(request.InitialPassword), request.Name, request.Role);

        await userRepository.AddAsync(user, ct);

        LogMessages.UserRegistered(logger, request.Email);

        // Publish domain event
        await mediator.Publish(new UserRegisteredEvent(user.Id, user.Email.Value, user.Name.Value, user.Role), ct);

        return user.Id;
    }
}
