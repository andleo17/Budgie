using Budgie.Modules.Relationships.Domain.Tags;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;

namespace Budgie.Modules.Relationships.Domain.Persons;

public class Person : Entity
{
    private const int MaxNameLength = 100;

    public Guid Id { get; internal set; }
    public Guid UserId { get; internal set; }
    public Name Name { get; internal set; } = null!;
    public string? PhotoPath { get; internal set; }
    public ICollection<Tag> Tags { get; internal set; } = new List<Tag>();

    public Person() { }

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
