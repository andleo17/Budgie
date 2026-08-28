using System.Data;
using Budgie.Modules.Finances.Application.Accounts;
using Budgie.Modules.Finances.Domain.Accounts;

namespace Budgie.Modules.Finances.Application;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct);
    Task<Account?> GetByIdRawAsync(Guid id, CancellationToken ct);
    Task<List<AccountResponse>> GetByUserIdAsync(Guid userId, CancellationToken ct);
    Task<Dictionary<string, Guid>> GetExternalUidMapAsync(Guid userId, CancellationToken ct);
    Task AddAsync(Account account, CancellationToken ct, IDbTransaction? tx = null);
    Task UpdateAsync(Account account, CancellationToken ct);
    Task DeleteAsync(Guid id, Guid userId, CancellationToken ct);
    Task UpdateBalanceAsync(Guid userId, Guid id, decimal newBalance, CancellationToken ct, IDbTransaction? tx = null);
}
