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
              ""BillingCycleDayInclusive"",""ControlStartDate"",""CreatedAt""
              FROM ""Accounts"" WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new { Id = id, UserId = userId }, ct);
        return row is null ? null : Map(row);
    }

    public async Task<Account?> GetByIdRawAsync(Guid id, CancellationToken ct)
    {
        var row = await factory.QuerySingleOrDefaultAsync<AccountRow>(
            @"SELECT ""Id"",""UserId"",""Name"",""Balance"",""Currency"",""Type"",
              ""CreditLimit"",""CreditLimitCurrency"",""CreditConsumptionMode"",""AnnualInterestRate"",
              ""BillingCycleDayInclusive"",""ControlStartDate"",""CreatedAt""
              FROM ""Accounts"" WHERE ""Id"" = @Id",
            new { Id = id }, ct);
        return row is null ? null : Map(row);
    }

    public async Task<List<AccountResponse>> GetByUserIdAsync(Guid userId, CancellationToken ct)
    {
        var rows = await factory.QueryListAsync<AccountResponseRow>(
            @"SELECT ""Id"",""Name"",""Balance"",""Currency"",""Type"",""CreditLimit"",""CreditLimitCurrency"",
              ""CreditConsumptionMode"",""AnnualInterestRate"",
              ""BillingCycleDayInclusive"",""BillingCycleDay"",""PaymentDueDay"",""InterestCalculationMethod"",""ControlStartDate"",""CreatedAt""
              FROM ""Accounts"" WHERE ""UserId"" = @UserId",
            new { UserId = userId }, ct);
        return rows.Select(r => new AccountResponse(
            r.Id, r.Name, r.Balance, r.Currency, r.Type,
            r.CreditLimit, r.CreditLimitCurrency, r.CreditConsumptionMode, r.AnnualInterestRate,
            r.BillingCycleDayInclusive, r.ControlStartDate, r.CreatedAt,
            r.BillingCycleDay, r.PaymentDueDay, r.InterestCalculationMethod)).ToList();
    }

    public async Task AddAsync(Account account, CancellationToken ct)
    {
        await factory.ExecuteAsync(
            @"INSERT INTO ""Accounts"" (""Id"",""UserId"",""Name"",""Balance"",""Currency"",""Type"",
              ""CreditLimit"",""CreditLimitCurrency"",""CreditConsumptionMode"",
              ""AnnualInterestRate"",""BillingCycleDayInclusive"",""ControlStartDate"",""CreatedAt"")
              VALUES (@Id,@UserId,@Name,@Balance,@Currency,@Type,
              @CreditLimit,@CreditLimitCurrency,@CreditConsumptionMode,
              @AnnualInterestRate,@BillingCycleDayInclusive,@ControlStartDate,@CreatedAt)",
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
                BillingCycleDayInclusive = account.CreditCardSettings?.BillingCycleDayInclusive ?? true,
                ControlStartDate = account.ControlStartDate,
                account.CreatedAt
            }, ct);
    }

    public Task UpdateAsync(Account account, CancellationToken ct)
        => factory.ExecuteAsync(
            @"UPDATE ""Accounts"" SET ""Name"" = @Name, ""CreditLimit"" = @CreditLimit,
              ""CreditLimitCurrency"" = @CreditLimitCurrency, ""CreditConsumptionMode"" = @CreditConsumptionMode,
              ""AnnualInterestRate"" = @AnnualInterestRate, ""BillingCycleDayInclusive"" = @BillingCycleDayInclusive,
              ""ControlStartDate"" = @ControlStartDate
              WHERE ""Id"" = @Id",
            new
            {
                Name = account.Name.Value,
                CreditLimit = (decimal?)account.CreditCardSettings?.CreditLimit.Amount,
                CreditLimitCurrency = account.CreditCardSettings?.CreditLimit.Currency?.Code,
                CreditConsumptionMode = (int)(account.CreditCardSettings?.CreditConsumptionMode ?? 0),
                AnnualInterestRate = account.CreditCardSettings is not null
                    ? (decimal?)account.CreditCardSettings.AnnualInterestRate.Value : null,
                BillingCycleDayInclusive = account.CreditCardSettings?.BillingCycleDayInclusive ?? true,
                ControlStartDate = account.ControlStartDate,
                account.Id
            }, ct);

    public Task DeleteAsync(Guid id, Guid userId, CancellationToken ct)
        => factory.ExecuteAsync(@"DELETE FROM ""Accounts"" WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new { Id = id, UserId = userId }, ct);

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
            CreatedAt = row.CreatedAt,
            ControlStartDate = row.ControlStartDate ?? row.CreatedAt.Date
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
        decimal? AnnualInterestRate, bool BillingCycleDayInclusive, DateTime? ControlStartDate, DateTime CreatedAt);

    private record AccountResponseRow(Guid Id, string Name, decimal Balance, string Currency,
        int Type, decimal? CreditLimit, string? CreditLimitCurrency, int CreditConsumptionMode,
        decimal? AnnualInterestRate, bool BillingCycleDayInclusive,
        int? BillingCycleDay, int? PaymentDueDay, int? InterestCalculationMethod,
        DateTime ControlStartDate, DateTime CreatedAt);
}
