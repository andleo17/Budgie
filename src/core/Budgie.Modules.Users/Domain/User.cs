using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;

namespace Budgie.Modules.Users.Domain;

public class User : Entity
{
    public Guid Id { get; internal set; }
    public EmailAddress Email { get; internal set; } = null!;
    public string PasswordHash { get; internal set; } = null!;
    public Name Name { get; internal set; } = null!;
    public UserRole Role { get; internal set; } = UserRole.User;

    public User() { }

    public static User Create(string email, string passwordHash, string name, UserRole role = UserRole.User)
    {
        Guard.NotNullOrEmpty(passwordHash, nameof(passwordHash));

        return new User
        {
            Id = Guid.NewGuid(),
            Email = EmailAddress.From(email),
            PasswordHash = passwordHash,
            Name = Name.From(name),
            Role = role
        };
    }

    public void Update(string name)
    {
        Name = Name.From(name);
    }

    public void SetRole(UserRole role)
    {
        Role = role;
    }
}
