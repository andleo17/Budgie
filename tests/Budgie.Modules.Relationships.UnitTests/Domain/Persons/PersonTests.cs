using Budgie.Modules.Relationships.Domain.Persons;
using Budgie.Shared.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Budgie.Modules.Relationships.UnitTests.Domain.Persons;

public class PersonTests
{
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public void Create_ShouldCreateWithValidName()
    {
        var person = Person.Create(_userId, "John Doe");

        person.Id.Should().NotBeEmpty();
        person.UserId.Should().Be(_userId);
        person.Name.Value.Should().Be("John Doe");
    }

    [Fact]
    public void Create_EmptyName_Throws()
    {
        var act = () => Person.Create(_userId, "");

        act.Should().Throw<ArgumentException>().WithMessage("Name cannot be null or empty.*");
    }

    [Fact]
    public void Create_NullName_Throws()
    {
        var act = () => Person.Create(_userId, null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WhitespaceName_Throws()
    {
        var act = () => Person.Create(_userId, "   ");

        act.Should().Throw<ArgumentException>().WithMessage("Name cannot be null or empty.*");
    }

    [Fact]
    public void Create_TrimsName()
    {
        var person = Person.Create(_userId, "  Jane Doe  ");

        person.Name.Value.Should().Be("Jane Doe");
    }

    [Fact]
    public void Create_MaxLengthEnforcement()
    {
        var longName = new string('a', 101);

        var act = () => Person.Create(_userId, longName);

        act.Should().Throw<ArgumentException>().WithMessage("Name cannot exceed 100 characters.*");
    }

    [Fact]
    public void Update_ShouldUpdateName()
    {
        var person = Person.Create(_userId, "Old Name");

        person.Update("New Name");

        person.Name.Value.Should().Be("New Name");
    }

    [Fact]
    public void Update_EmptyName_Throws()
    {
        var person = Person.Create(_userId, "Valid");

        var act = () => person.Update("");

        act.Should().Throw<ArgumentException>().WithMessage("Name cannot be null or empty.*");
    }

    [Fact]
    public void SetPhotoPath_SetsPathCorrectly()
    {
        var person = Person.Create(_userId, "Photo Person");

        person.SetPhotoPath("/photos/avatar.jpg");

        person.PhotoPath.Should().Be("/photos/avatar.jpg");
    }

    [Fact]
    public void SetPhotoPath_ClearsPathWithNull()
    {
        var person = Person.Create(_userId, "Photo Person");
        person.SetPhotoPath("/photos/avatar.jpg");

        person.SetPhotoPath(null);

        person.PhotoPath.Should().BeNull();
    }
}
