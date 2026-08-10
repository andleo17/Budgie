using Budgie.Shared.Abstractions;
using Budgie.Shared.Infrastructure;

namespace Budgie.Modules.Finances.Infrastructure;

internal sealed class CurrencyUsageChecker(IDbConnectionFactory factory) : ICurrencyUsageChecker
{
    public async Task<List<string>> GetBlockingAccountNamesAsync(Guid userId, string currencyCode)
    {
        var accounts = await factory.QueryListAsync<string>(
            @"SELECT ""Name"" FROM ""Accounts"" WHERE ""UserId"" = @UserId AND ""Currency"" = @Currency",
            new { UserId = userId, Currency = currencyCode });
        return accounts.ToList();
    }
}
