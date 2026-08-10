using System.Text.Json;
using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Application.Accounts;
using Budgie.Modules.Finances.Domain.Accounts;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using Budgie.Shared.Infrastructure;

namespace Budgie.Modules.Finances.Infrastructure.Database;

internal sealed class AccountRepository(IDbConnectionFactory factory) : IAccountRepository
{
    public async Task<Account?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var row = await factory.QuerySingleOrDefaultAsync<AccountRow>(
            @"SELECT ""Id"",""UserId"",""Name"",""Balance"",""Currency"",""Type"",
              ""CreditLimit"",""CreditLimitCurrency"",""CreditConsumptionMode"",""AnnualInterestRate"",
              ""BillingCycleDayInclusive"",""CreatedAt""
              FROM ""Accounts"" WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new { Id = id, UserId = userId }, ct);
        return row is null ? null : Map(row);
    }

    public async Task<Account?> GetByIdRawAsync(Guid id, CancellationToken ct)
    {
        var row = await factory.QuerySingleOrDefaultAsync<AccountRow>(
            @"SELECT ""Id"",""UserId"",""Name"",""Balance"",""Currency"",""Type"",
              ""CreditLimit"",""CreditLimitCurrency"",""CreditConsumptionMode"",""AnnualInterestRate"",
              ""BillingCycleDayInclusive"",""CreatedAt""
              FROM ""Accounts"" WHERE ""Id"" = @Id",
            new { Id = id }, ct);
        return row is null ? null : Map(row);
    }

    public async Task<List<AccountResponse>> GetByUserIdAsync(Guid userId, CancellationToken ct)
    {
        return (await factory.QueryListAsync<AccountResponse>(
            @"SELECT ""Id"",""Name"",""Balance"",""Currency"",""Type"",""CreditLimit"",""CreditLimitCurrency"",
              ""CreditConsumptionMode"" AS CreditConsumptionMode,""AnnualInterestRate"" AS AnnualInterestRate,
              ""BillingCycleDayInclusive"",""CreatedAt""
              FROM ""Accounts"" WHERE ""UserId"" = @UserId",
            new { UserId = userId }, ct)).ToList();
    }

    public async Task AddAsync(Account account, CancellationToken ct)
    {
        await factory.ExecuteAsync(
            @"INSERT INTO ""Accounts"" (""Id"",""UserId"",""Name"",""Balance"",""Currency"",""Type"",
              ""CreditLimit"",""CreditLimitCurrency"",""CreditConsumptionMode"",
              ""AnnualInterestRate"",""BillingCycleDayInclusive"",""CreatedAt"")
              VALUES (@Id,@UserId,@Name,@Balance,@Currency,@Type,
              @CreditLimit,@CreditLimitCurrency,@CreditConsumptionMode,
              @AnnualInterestRate,@BillingCycleDayInclusive,@CreatedAt)",
            new
            {
                account.Id, account.UserId, Name = account.Name.Value,
                Balance = account.Balance.Amount, Currency = account.Balance.Currency.Code,
                Type = (int)account.Type,
                CreditLimit = (decimal?)account.CreditCardSettings?.CreditLimit.Amount,
                CreditLimitCurrency = account.CreditCardSettings?.CreditLimit.Currency?.Code,
                CreditConsumptionMode = (int)(account.CreditCardSettings?.CreditConsumptionMode ?? 0),
                AnnualInterestRate = account.CreditCardSettings is not null
                    ? (decimal?)account.CreditCardSettings.AnnualInterestRate.Value : null,
                BillingCycleDayInclusive = account.CreditCardSettings?.BillingCycleDayInclusive,
                account.CreatedAt
            }, ct);
    }

    public Task UpdateBalanceAsync(Guid id, decimal newBalance, CancellationToken ct)
        => factory.ExecuteAsync(
            @"UPDATE ""Accounts"" SET ""Balance"" = @Balance WHERE ""Id"" = @Id",
            new { Balance = newBalance, Id = id }, ct);

    private static Account Map(AccountRow row)
    {
        var account = new Account
        {
            Id = row.Id,
            UserId = row.UserId,
            Name = Name.From(row.Name),
            Balance = Money.FromAny(row.Balance, CurrencyCode.From(row.Currency)),
            Type = (AccountType)row.Type,
            CreatedAt = row.CreatedAt
        };
        if (row.CreditLimit.HasValue)
        {
            account.CreditCardSettings = new CreditCardSettings(
                row.CreditLimit.Value,
                (Shared.Enums.CreditConsumptionMode)row.CreditConsumptionMode,
                null, null, row.AnnualInterestRate ?? 0,
                row.BillingCycleDayInclusive,
                CurrencyCode.From(row.CreditLimitCurrency ?? row.Currency));
        }
        return account;
    }

    private record AccountRow(Guid Id, Guid UserId, string Name, decimal Balance, string Currency,
        int Type, decimal? CreditLimit, string? CreditLimitCurrency, int CreditConsumptionMode,
        decimal? AnnualInterestRate, bool BillingCycleDayInclusive, DateTime CreatedAt);
}
