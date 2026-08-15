using Budgie.Modules.Relationships.Application.Persons;

namespace Budgie.Modules.Relationships.Application;

public interface IPersonRepository
{
    Task<PersonResponse?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct);
    Task<List<PersonResponse>> GetByUserIdAsync(Guid userId, CancellationToken ct);
    Task AddAsync(Guid id, Guid userId, string name, List<string>? tagNames, CancellationToken ct);
    Task<bool> UpdateAsync(Guid id, Guid userId, string name, List<string>? tagNames, CancellationToken ct);
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct);
    Task<bool> UpdatePhotoPathAsync(Guid id, Guid userId, string? photoPath, CancellationToken ct);
}
