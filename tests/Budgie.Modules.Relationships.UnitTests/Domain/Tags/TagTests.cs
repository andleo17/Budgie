using Budgie.Modules.Relationships.Domain.Tags;
using Budgie.Shared.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Budgie.Modules.Relationships.UnitTests.Domain.Tags;

public class TagTests
{
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public void Create_ShouldCreateWithValidName()
    {
        var tag = Tag.Create(_userId, "Friend");

        tag.Id.Should().NotBeEmpty();
        tag.UserId.Should().Be(_userId);
        tag.Name.Value.Should().Be("Friend");
        tag.Color.Should().BeNull();
    }

    [Fact]
    public void Create_WithColor_StoresColor()
    {
        var tag = Tag.Create(_userId, "Family", "#FF0000");

        tag.Color.Should().NotBeNull();
        tag.Color!.Value.Should().Be("#FF0000");
    }

    [Fact]
    public void Create_EmptyName_Throws()
    {
        var act = () => Tag.Create(_userId, "");

        act.Should().Throw<ArgumentException>().WithMessage("Name cannot be null or empty.*");
    }

    [Fact]
    public void Create_NullName_Throws()
    {
        var act = () => Tag.Create(_userId, null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_TrimsName()
    {
        var tag = Tag.Create(_userId, "  Work  ");

        tag.Name.Value.Should().Be("Work");
    }

    [Fact]
    public void Create_ValidatesHexColor()
    {
        var act = () => Tag.Create(_userId, "Bad", "not-a-color");

        act.Should().Throw<ArgumentException>().WithMessage("Color must be in #RRGGBB format.*");
    }

    [Fact]
    public void Create_InvalidHexLength_Throws()
    {
        var act = () => Tag.Create(_userId, "Bad", "#FFF");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Update_ShouldUpdateNameAndColor()
    {
        var tag = Tag.Create(_userId, "Old", "#000000");

        tag.Update("Updated", "#FFFFFF");

        tag.Name.Value.Should().Be("Updated");
        tag.Color!.Value.Should().Be("#FFFFFF");
    }

    [Fact]
    public void Update_ShouldClearColor()
    {
        var tag = Tag.Create(_userId, "Old", "#000000");

        tag.Update("Updated", null);

        tag.Name.Value.Should().Be("Updated");
        tag.Color.Should().BeNull();
    }

    [Fact]
    public void Update_EmptyName_Throws()
    {
        var tag = Tag.Create(_userId, "Valid");

        var act = () => tag.Update("", null);

        act.Should().Throw<ArgumentException>().WithMessage("Name cannot be null or empty.*");
    }
}
