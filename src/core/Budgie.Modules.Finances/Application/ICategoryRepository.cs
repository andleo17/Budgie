using Budgie.Modules.Finances.Domain.Categories;

namespace Budgie.Modules.Finances.Application;

public record CategoryResponse(
    Guid Id,
    string Name,
    string? Color,
    string? Icon,
    int Type,
    bool IsSystem,
    Guid? ParentId,
    decimal? MonthlyBudget = null);

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct);
    Task<List<Category>> GetByUserIdAsync(Guid userId, CancellationToken ct);
    Task<List<CategoryResponse>> GetResponsesByUserIdAsync(Guid userId, bool includeSystem, CancellationToken ct);
    Task AddAsync(Category category, CancellationToken ct);
    Task UpdateAsync(Category category, CancellationToken ct);
    Task DeleteAsync(Guid id, Guid userId, CancellationToken ct);
}
