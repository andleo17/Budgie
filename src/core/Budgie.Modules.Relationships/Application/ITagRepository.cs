using Budgie.Modules.Relationships.Application.Tags;

namespace Budgie.Modules.Relationships.Application;

public interface ITagRepository
{
    Task<List<TagResponse>> GetByUserIdAsync(Guid userId, CancellationToken ct);
    Task<Guid> AddAsync(Guid userId, string name, string? color, CancellationToken ct);
    Task<bool> UpdateAsync(Guid id, Guid userId, string name, string? color, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct);
}
