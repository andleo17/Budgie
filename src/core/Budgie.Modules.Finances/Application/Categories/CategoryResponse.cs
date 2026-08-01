namespace Budgie.Modules.Finances.Application.Categories;

public record CategoryResponse(Guid Id, string Name, string? Color, string? Icon, int Type, Guid? ParentId, bool IsSystem = false, List<CategoryResponse>? Children = null);
