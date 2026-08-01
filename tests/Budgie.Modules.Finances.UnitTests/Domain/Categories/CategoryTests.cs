using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Shared.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Budgie.Modules.Finances.UnitTests.Domain.Categories;

public class CategoryTests
{
    [Fact]
    public void Create_ShouldCreateWithValidParameters()
    {
        var userId = Guid.NewGuid();
        var category = Category.Create(userId, "Food", 0, "#ff0000", "utensils");

        category.Id.Should().NotBeEmpty();
        category.UserId.Should().Be(userId);
        category.Name.Value.Should().Be("Food");
        category.Color.Should().NotBeNull();
        category.Color!.Value.Should().Be("#FF0000");
        category.Icon.Should().Be("utensils");
        category.Type.Should().Be(0);
        category.IsSystem.Should().BeFalse();
        category.ParentId.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldCreateWithOptionalParameters()
    {
        var userId = Guid.NewGuid();
        var category = Category.Create(userId, "Food", 0);

        category.Color.Should().BeNull();
        category.Icon.Should().BeNull();
        category.ParentId.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldThrow_WhenNameIsEmpty()
    {
        var act = () => Category.Create(Guid.NewGuid(), "", 0);
        act.Should().Throw<ArgumentException>().WithMessage("Name cannot be null or empty.*");
    }

    [Fact]
    public void Create_ShouldTrimName()
    {
        var category = Category.Create(Guid.NewGuid(), "  Food  ", 0);
        category.Name.Value.Should().Be("Food");
    }

    [Fact]
    public void Create_ShouldNormalizeHexColor()
    {
        var category = Category.Create(Guid.NewGuid(), "Food", 0, "#Ff0000");
        category.Color!.Value.Should().Be("#FF0000");
    }

    [Fact]
    public void CreateSystem_ShouldCreateWithoutUserId()
    {
        var id = SystemCategoryIds.Transfer;
        var category = Category.CreateSystem(id, "Transfer", 1);

        category.Id.Should().Be(id);
        category.UserId.Should().BeNull();
        category.Name.Value.Should().Be("Transfer");
        category.Type.Should().Be(1);
        category.IsSystem.Should().BeTrue();
    }

    [Fact]
    public void Update_ShouldChangeProperties()
    {
        var category = Category.Create(Guid.NewGuid(), "Food", 0, "#ff0000", "old-icon");
        var parentId = Guid.NewGuid();

        category.Update("Transport", "#00ff00", "car", 1, parentId);

        category.Name.Value.Should().Be("Transport");
        category.Color!.Value.Should().Be("#00FF00");
        category.Icon.Should().Be("car");
        category.Type.Should().Be(1);
        category.ParentId.Should().Be(parentId);
    }

    [Fact]
    public void Update_ShouldThrow_WhenNameIsEmpty()
    {
        var category = Category.Create(Guid.NewGuid(), "Food", 0);
        var act = () => category.Update("", null, null);
        act.Should().Throw<ArgumentException>().WithMessage("Name cannot be null or empty.*");
    }

    [Fact]
    public void HasCircularReference_ShouldReturnTrue_WhenTargetIsOwnId()
    {
        var category = Category.Create(Guid.NewGuid(), "Root", 0);
        var map = new Dictionary<Guid, Category?> { { category.Id, category } };

        var result = category.HasCircularReference(category.Id, map);

        result.Should().BeTrue();
    }

    [Fact]
    public void HasCircularReference_ShouldReturnFalse_WhenTargetNotInMap()
    {
        var category = Category.Create(Guid.NewGuid(), "Root", 0);
        var map = new Dictionary<Guid, Category?>();

        var result = category.HasCircularReference(Guid.NewGuid(), map);

        result.Should().BeFalse();
    }

    [Fact]
    public void GetDepth_ShouldReturnZero_ForRootCategory()
    {
        var category = Category.Create(Guid.NewGuid(), "Root", 0);
        var map = new Dictionary<Guid, Category?> { { category.Id, category } };

        var depth = category.GetDepth(map);

        depth.Should().Be(0);
    }

    [Fact]
    public void GetDepth_ShouldReturnCorrectDepth_ForNestedCategories()
    {
        var root = Category.Create(Guid.NewGuid(), "Root", 0);
        var child = Category.Create(Guid.NewGuid(), "Child", 0, parentId: root.Id);
        var grandchild = Category.Create(Guid.NewGuid(), "Grandchild", 0, parentId: child.Id);

        var map = new Dictionary<Guid, Category?>
        {
            { root.Id, root },
            { child.Id, child },
            { grandchild.Id, grandchild }
        };

        grandchild.GetDepth(map).Should().Be(2);
        child.GetDepth(map).Should().Be(1);
        root.GetDepth(map).Should().Be(0);
    }
}
