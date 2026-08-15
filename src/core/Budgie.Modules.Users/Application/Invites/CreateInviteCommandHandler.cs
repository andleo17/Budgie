using Budgie.Modules.Users.Application;
using Budgie.Modules.Users.Domain;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Enums;
using ErrorOr;
using Mediator;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Budgie.Modules.Users.Application.Invites;

public record CreateInviteCommand(string? Email, int ExpiresInHours = 48) : IRequest<ErrorOr<CreateInviteResponse>>;

public record CreateInviteResponse(Guid Id, string Token, string? Email, DateTime ExpiresAt);

internal sealed class CreateInviteCommandHandler(
    IUserInviteRepository inviteRepository,
    IUserRepository userRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<CreateInviteCommand, ErrorOr<CreateInviteResponse>>
{
    public async ValueTask<ErrorOr<CreateInviteResponse>> Handle(CreateInviteCommand request, CancellationToken ct)
    {
        var currentUserId = currentUserProvider.GetUserId();
        var currentUser = await userRepository.GetByIdAsync(currentUserId, ct);

        if (currentUser is null || currentUser.Role != UserRole.Admin)
        {
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en"
                ? Error.Forbidden("Auth.AdminRequired", "Admin privileges required to create invites.")
                : Error.Forbidden("Auth.AdminRequired", "Se requieren privilegios de Administrador para crear invitaciones.");
        }

        var rawToken = $"inv_{Guid.NewGuid():N}{RandomNumberGenerator.GetHexString(16)}";
        var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
        var expiresAt = DateTime.UtcNow.AddHours(Math.Max(1, request.ExpiresInHours));

        var invite = UserInvite.Create(tokenHash, request.Email, expiresAt, currentUserId);
        await inviteRepository.AddAsync(invite, ct);

        return new CreateInviteResponse(invite.Id, rawToken, request.Email, expiresAt);
    }
}
