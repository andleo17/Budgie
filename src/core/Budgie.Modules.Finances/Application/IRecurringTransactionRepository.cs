using System.Data;
using Budgie.Modules.Finances.Application.RecurringTransactions.Shared;
using Budgie.Modules.Finances.Domain.RecurringTransactions;
using Budgie.Shared.Enums;

namespace Budgie.Modules.Finances.Application;

public interface IRecurringTransactionRepository
{
    Task<RecurringTransaction?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct);
    Task<Dictionary<string, Guid>> GetExternalUidMapAsync(Guid userId, CancellationToken ct);
    Task<List<RecurringTransaction>> GetByUserIdAsync(Guid userId, RecurringApplicability? applicability, bool? isActive, CancellationToken ct);
    Task<List<RecurringTransaction>> GetDueByUserIdAsync(Guid userId, CancellationToken ct);
    Task<List<DueRecurringResponse>> GetDueResponsesAsync(Guid userId, CancellationToken ct);
    Task AddAsync(RecurringTransaction template, CancellationToken ct, IDbTransaction? tx = null);
    Task AddParticipantsAsync(RecurringTransaction template, CancellationToken ct);
    Task UpdateAsync(RecurringTransaction template, CancellationToken ct);
    Task ReplaceParticipantsAsync(RecurringTransaction template, CancellationToken ct);
    Task UpdateApplyStateAsync(RecurringTransaction template, CancellationToken ct);
    Task DeleteAsync(Guid id, Guid userId, CancellationToken ct);
}
