using Budgie.Modules.Relationships.Domain.Persons;
using Budgie.Shared.Domain.ValueObjects;

namespace Budgie.Modules.Relationships.Domain.Tags;

public class Tag
{
    private const int MaxNameLength = 50;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Name Name { get; private set; } = null!;
    public HexColor? Color { get; private set; }
    public ICollection<Person> Persons { get; private set; } = new List<Person>();

    private Tag() { }

    public static Tag Create(Guid userId, string name, string? color = null)
    {
        return new Tag
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = Name.From(name, MaxNameLength),
            Color = HexColor.FromNullable(color)
        };
    }

    public void Update(string name, string? color)
    {
        Name = Name.From(name, MaxNameLength);
        Color = HexColor.FromNullable(color);
    }
}
