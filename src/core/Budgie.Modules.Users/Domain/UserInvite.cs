using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;

namespace Budgie.Modules.Users.Domain;

public class UserInvite : Entity
{
    public Guid Id { get; internal set; }
    public string TokenHash { get; internal set; } = null!;
    public EmailAddress? Email { get; internal set; }
    public DateTime ExpiresAt { get; internal set; }
    public DateTime CreatedAt { get; internal set; }
    public bool IsUsed { get; internal set; }
    public Guid CreatedByUserId { get; internal set; }

    public UserInvite() { }

    public static UserInvite Create(string tokenHash, string? email, DateTime expiresAt, Guid createdByUserId)
    {
        return new UserInvite
        {
            Id = Guid.NewGuid(),
            TokenHash = tokenHash,
            Email = string.IsNullOrWhiteSpace(email) ? null : EmailAddress.From(email),
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow,
            IsUsed = false,
            CreatedByUserId = createdByUserId
        };
    }

    public bool IsValid => !IsUsed && ExpiresAt > DateTime.UtcNow;

    public void MarkAsUsed() => IsUsed = true;
}
