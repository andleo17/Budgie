using Budgie.Modules.Relationships.Domain.Tags;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;

namespace Budgie.Modules.Relationships.Domain.Persons;

public class Person : Entity
{
    private const int MaxNameLength = 100;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Name Name { get; private set; } = null!;
    public string? PhotoPath { get; private set; }
    public ICollection<Tag> Tags { get; private set; } = new List<Tag>();

    private Person() { }

    public static Person Create(Guid userId, string name)
    {
        return new Person
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = Name.From(name, MaxNameLength)
        };
    }

    public void Update(string name)
    {
        Name = Name.From(name, MaxNameLength);
    }

    public void SetPhotoPath(string? path)
    {
        PhotoPath = path;
    }
}
