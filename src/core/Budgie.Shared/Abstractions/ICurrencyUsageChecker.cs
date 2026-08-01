namespace Budgie.Shared.Abstractions;

public interface ICurrencyUsageChecker
{
    Task<List<string>> GetBlockingAccountNamesAsync(Guid userId, string currencyCode);
}
