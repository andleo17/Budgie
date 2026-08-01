using Budgie.Shared.Abstractions;

namespace Budgie.Modules.Users.Domain;

public class RefreshToken : Entity
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public bool IsRevoked { get; private set; }

    private RefreshToken() { }

    public static RefreshToken Create(Guid userId, string tokenHash, DateTime expiresAt)
    {
        Guard.NotNullOrEmpty(tokenHash, nameof(tokenHash));

        if (expiresAt <= DateTime.UtcNow)
            throw new ArgumentException("Expiration date must be in the future.", nameof(expiresAt));

        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = Guard.NormalizeDate(expiresAt),
            CreatedAt = Guard.NormalizeDate(DateTime.UtcNow),
            IsRevoked = false
        };
    }

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    public void Revoke()
    {
        IsRevoked = true;
    }
}
