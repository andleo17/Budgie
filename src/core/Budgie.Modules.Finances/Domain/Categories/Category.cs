using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;

namespace Budgie.Modules.Finances.Domain.Categories;

public class Category : Entity
{
    private const int MaxNameLength = 50;
    public const int MaxDepth = 3;

    public Guid Id { get; internal set; }
    public Guid? UserId { get; internal set; }
    public Name Name { get; internal set; } = null!;
    public HexColor? Color { get; internal set; }
    public string? Icon { get; internal set; }
    public int Type { get; internal set; }
    public bool IsSystem { get; internal set; }
    public decimal? MonthlyBudget { get; internal set; }
    public Guid? ParentId { get; internal set; }
    public Category? Parent { get; internal set; }
    public ICollection<Category> Children { get; internal set; } = [];

    public Category() { }

    public static Category Create(Guid userId, string name, int type, string? color = null, string? icon = null, Guid? parentId = null, decimal? monthlyBudget = null)
    {
        return new Category
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = Name.From(name, MaxNameLength),
            Color = HexColor.FromNullable(color),
            Icon = icon,
            Type = type,
            ParentId = parentId,
            MonthlyBudget = monthlyBudget.HasValue && monthlyBudget.Value > 0 ? monthlyBudget.Value : null
        };
    }

    public static Category CreateSystem(Guid id, string name, int type)
    {
        return new Category
        {
            Id = id,
            UserId = null,
            Name = Name.From(name, MaxNameLength),
            Type = type,
            IsSystem = true
        };
    }

    public void Update(string name, string? color, string? icon, int? type = null, Guid? parentId = null, decimal? monthlyBudget = null)
    {
        Name = Name.From(name, MaxNameLength);
        Color = HexColor.FromNullable(color);
        Icon = icon;
        if (type.HasValue)
            Type = type.Value;
        ParentId = parentId;
        MonthlyBudget = monthlyBudget.HasValue && monthlyBudget.Value > 0 ? monthlyBudget.Value : null;
    }

    public bool HasCircularReference(Guid targetParentId, Dictionary<Guid, Category?> categoryMap)
    {
        var current = targetParentId;
        while (true)
        {
            if (current == Id)
                return true;

            if (!categoryMap.TryGetValue(current, out var ancestor) || ancestor is null)
                break;

            if (!ancestor.ParentId.HasValue)
                break;
            current = ancestor.ParentId.Value;
        }
        return false;
    }

    public int GetDepth(Dictionary<Guid, Category?> categoryMap)
    {
        var depth = 0;
        var current = ParentId;

        while (current.HasValue)
        {
            depth++;
            if (!categoryMap.TryGetValue(current.Value, out var parent) || parent is null)
                break;
            current = parent.ParentId;
        }

        return depth;
    }

    public int GetSubtreeMaxDepth(Dictionary<Guid, Category?> categoryMap)
    {
        var maxDepth = GetDepth(categoryMap);
        var queue = new Queue<Guid>();

        foreach (var (id, cat) in categoryMap)
        {
            if (cat?.ParentId == Id)
                queue.Enqueue(id);
        }

        while (queue.Count > 0)
        {
            var childId = queue.Dequeue();
            if (categoryMap.TryGetValue(childId, out var child) && child is not null)
            {
                var childDepth = child.GetDepth(categoryMap);
                if (childDepth > maxDepth)
                    maxDepth = childDepth;

                foreach (var (id, cat) in categoryMap)
                {
                    if (cat?.ParentId == childId)
                        queue.Enqueue(id);
                }
            }
        }

        return maxDepth;
    }
}
