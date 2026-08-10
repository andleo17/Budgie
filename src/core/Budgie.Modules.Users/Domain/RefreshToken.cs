using Budgie.Shared.Abstractions;

namespace Budgie.Modules.Users.Domain;

public class RefreshToken : Entity
{
    public Guid Id { get; internal set; }
    public Guid UserId { get; internal set; }
    public string TokenHash { get; internal set; } = null!;
    public DateTime ExpiresAt { get; internal set; }
    public DateTime CreatedAt { get; internal set; }
    public bool IsRevoked { get; internal set; }

    public RefreshToken() { }

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
