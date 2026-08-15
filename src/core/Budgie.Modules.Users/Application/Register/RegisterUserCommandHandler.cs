using Budgie.Modules.Users.Application;
using Budgie.Modules.Users.Domain;
using Budgie.Modules.Users.Domain.Events;
using Budgie.Modules.Users.Infrastructure.Logging;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BC = BCrypt.Net.BCrypt;

namespace Budgie.Modules.Users.Application.Register;

internal sealed class RegisterUserCommandHandler(
    IUserRepository userRepository,
    IUserInviteRepository inviteRepository,
    IRegistrationPolicy registrationPolicy,
    IMediator mediator,
    ILogger<RegisterUserCommandHandler> logger)
    : IRequestHandler<RegisterUserCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(RegisterUserCommand request, CancellationToken ct)
    {
        var hasUsers = await userRepository.AnyAsync(ct);
        UserRole assignedRole;
        UserInvite? validInvite = null;

        if (!hasUsers)
        {
            // First user automatically becomes Admin
            assignedRole = UserRole.Admin;
        }
        else if (!string.IsNullOrWhiteSpace(request.InviteToken))
        {
            var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(request.InviteToken.Trim())));
            validInvite = await inviteRepository.GetByHashAsync(tokenHash, ct);

            if (validInvite is null || !validInvite.IsValid)
            {
                return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
                    ? Error.Validation("Invite.Invalid", "Invitation token is invalid or expired.")
                    : Error.Validation("Invite.Invalid", "El token de invitación es inválido o ha expirado.");
            }

            if (validInvite.Email is not null && !string.Equals(validInvite.Email.Value, request.Email.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
                    ? Error.Validation("Invite.EmailMismatch", "The registration email does not match the invitation email.")
                    : Error.Validation("Invite.EmailMismatch", "El correo de registro no coincide con el correo invitado.");
            }

            assignedRole = UserRole.User;
        }
        else
        {
            if (!await registrationPolicy.CanRegisterAsync(ct))
            {
                return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
                    ? Error.Forbidden("User.RegistrationClosed", "Registration is closed. An invitation is required.")
                    : Error.Forbidden("User.RegistrationClosed", "El registro público está cerrado. Se requiere una invitación.");
            }

            assignedRole = UserRole.User;
        }

        if (await userRepository.EmailExistsAsync(request.Email, ct))
        {
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
                ? Error.Conflict("User.EmailAlreadyExists", "Email is already in use.")
                : Error.Conflict("User.EmailAlreadyExists", "El correo electrónico ya está en uso.");
        }

        var user = User.Create(request.Email, BC.HashPassword(request.Password), request.Name, assignedRole);

        await userRepository.AddAsync(user, ct);

        if (validInvite is not null)
        {
            await inviteRepository.MarkAsUsedAsync(validInvite.Id, ct);
        }

        LogMessages.UserRegistered(logger, request.Email);

        // Publish domain event
        await mediator.Publish(new UserRegisteredEvent(user.Id, user.Email.Value, user.Name.Value, user.Role), ct);

        return user.Id;
    }
}
