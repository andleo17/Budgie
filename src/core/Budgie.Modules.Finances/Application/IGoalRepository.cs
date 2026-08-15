using Budgie.Modules.Finances.Application.Goals;
using Budgie.Modules.Finances.Domain.Goals;

namespace Budgie.Modules.Finances.Application;

public interface IGoalRepository
{
    Task<Goal?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct);
    Task<List<GoalResponse>> GetGoalsByUserIdAsync(Guid userId, CancellationToken ct);
    Task AddAsync(Goal goal, CancellationToken ct);
    Task UpdateAsync(Goal goal, CancellationToken ct);
    Task DeleteAsync(Guid id, Guid userId, CancellationToken ct);
}
