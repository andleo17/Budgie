using Budgie.Modules.Users.Domain;
using ErrorOr;

namespace Budgie.Modules.Users.Application;

public interface IUserRepository
{
    Task<bool> AnyAsync(CancellationToken ct);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct);
    Task AddAsync(User user, CancellationToken ct);
    Task UpdateAsync(User user, CancellationToken ct);
}

public interface IUserSettingsRepository
{
    Task<UserSettings?> GetByUserIdAsync(Guid userId, CancellationToken ct);
    Task AddAsync(UserSettings settings, CancellationToken ct);
    Task UpdateAsync(UserSettings settings, CancellationToken ct);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct);
    Task AddAsync(RefreshToken token, CancellationToken ct);
    Task RevokeAsync(Guid id, CancellationToken ct);
}

public interface IUserInviteRepository
{
    Task<UserInvite?> GetByHashAsync(string tokenHash, CancellationToken ct);
    Task AddAsync(UserInvite invite, CancellationToken ct);
    Task MarkAsUsedAsync(Guid id, CancellationToken ct);
    Task<List<UserInvite>> GetActiveInvitesAsync(CancellationToken ct);
}

