using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Infrastructure;

internal sealed class CurrencyUsageChecker(FinancesDbContext dbContext) : ICurrencyUsageChecker
{
    public async Task<List<string>> GetBlockingAccountNamesAsync(Guid userId, string currencyCode)
    {
        return await dbContext.Accounts
            .Where(a => a.UserId == userId && a.Balance.Currency == currencyCode && a.Balance.Amount != 0)
            .Select(a => (string)a.Name)
            .ToListAsync();
    }
}
