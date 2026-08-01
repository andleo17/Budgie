using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;

namespace Budgie.Modules.Users.Domain;

public class User : Entity
{
    public Guid Id { get; private set; }
    public EmailAddress Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public Name Name { get; private set; } = null!;

    private User() { }

    public static User Create(string email, string passwordHash, string name)
    {
        Guard.NotNullOrEmpty(passwordHash, nameof(passwordHash));

        return new User
        {
            Id = Guid.NewGuid(),
            Email = EmailAddress.From(email),
            PasswordHash = passwordHash,
            Name = Name.From(name)
        };
    }

    public void Update(string name)
    {
        Name = Name.From(name);
    }
}
