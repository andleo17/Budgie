using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Budgie.Modules.Users.Domain;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Infrastructure;

namespace Budgie.Modules.Users.Infrastructure.Database;

internal sealed class UserSettingsRepository(IDbConnectionFactory factory) : Application.IUserSettingsRepository
{
    [UnconditionalSuppressMessage("AOT", "IL3050")]
    [UnconditionalSuppressMessage("Trimming", "IL2026")]
    private static string Serialize(List<string> currencies)
        => JsonSerializer.Serialize(currencies);

    [UnconditionalSuppressMessage("AOT", "IL3050")]
    [UnconditionalSuppressMessage("Trimming", "IL2026")]
    private static List<string> Deserialize(string? json)
        => string.IsNullOrEmpty(json) ? ["USD"] : JsonSerializer.Deserialize<List<string>>(json) ?? ["USD"];

    public async Task<UserSettings?> GetByUserIdAsync(Guid userId, CancellationToken ct)
    {
        var row = await factory.QuerySingleOrDefaultAsync<SettingsRow>(
            @"SELECT ""UserId"",""MainCurrency"",""DefaultCreditConsumptionMode"",""Language"",""IsConfigured"",
              ""SupportedCurrencies""::text AS SupportedCurrencies,""TransactionPageSize"",
              ""DefaultRecurringApplicability"",""AutoApplyOnLoad""
              FROM ""UserSettings"" WHERE ""UserId"" = @UserId",
            new { UserId = userId }, ct);
        return row is null ? null : Map(row);
    }

    public async Task AddAsync(UserSettings settings, CancellationToken ct)
        => await factory.ExecuteAsync(
            @"INSERT INTO ""UserSettings"" (""UserId"",""MainCurrency"",""DefaultCreditConsumptionMode"",""Language"",""IsConfigured"",""SupportedCurrencies"",""TransactionPageSize"",""DefaultRecurringApplicability"",""AutoApplyOnLoad"")
              VALUES (@UserId,@MainCurrency,@DefaultCreditConsumptionMode,@Language,@IsConfigured,@SupportedCurrencies::jsonb,@TransactionPageSize,@DefaultRecurringApplicability,@AutoApplyOnLoad)",
            new
            {
                settings.UserId,
                MainCurrency = settings.MainCurrency.Code,
                DefaultCreditConsumptionMode = (int)settings.DefaultCreditConsumptionMode,
                settings.Language,
                settings.IsConfigured,
                SupportedCurrencies = Serialize(settings.SupportedCurrencies),
                settings.TransactionPageSize,
                DefaultRecurringApplicability = (int)settings.DefaultRecurringApplicability,
                AutoApplyOnLoad = settings.AutoApplyOnLoad
            }, ct);

    public async Task UpdateAsync(UserSettings settings, CancellationToken ct)
        => await factory.ExecuteAsync(
            @"UPDATE ""UserSettings"" SET ""MainCurrency"" = @MainCurrency,""DefaultCreditConsumptionMode"" = @DefaultCreditConsumptionMode,
              ""Language"" = @Language,""IsConfigured"" = @IsConfigured,
              ""SupportedCurrencies"" = @SupportedCurrencies::jsonb,
              ""TransactionPageSize"" = @TransactionPageSize,
              ""DefaultRecurringApplicability"" = @DefaultRecurringApplicability,
              ""AutoApplyOnLoad"" = @AutoApplyOnLoad
              WHERE ""UserId"" = @UserId",
            new
            {
                settings.UserId,
                MainCurrency = settings.MainCurrency.Code,
                DefaultCreditConsumptionMode = (int)settings.DefaultCreditConsumptionMode,
                settings.Language,
                settings.IsConfigured,
                SupportedCurrencies = Serialize(settings.SupportedCurrencies),
                settings.TransactionPageSize,
                DefaultRecurringApplicability = (int)settings.DefaultRecurringApplicability,
                AutoApplyOnLoad = settings.AutoApplyOnLoad
            }, ct);

    private static UserSettings Map(SettingsRow row) => new()
    {
        UserId = row.UserId,
        MainCurrency = CurrencyCode.From(row.MainCurrency),
        DefaultCreditConsumptionMode = (Budgie.Shared.Enums.CreditConsumptionMode)row.DefaultCreditConsumptionMode,
        Language = row.Language,
        IsConfigured = row.IsConfigured,
        SupportedCurrencies = Deserialize(row.SupportedCurrencies),
        TransactionPageSize = row.TransactionPageSize,
        DefaultRecurringApplicability = (Budgie.Shared.Enums.RecurringApplicability)row.DefaultRecurringApplicability,
        AutoApplyOnLoad = row.AutoApplyOnLoad
    };

    private record SettingsRow(Guid UserId, string MainCurrency, int DefaultCreditConsumptionMode,
        string Language, bool IsConfigured, string SupportedCurrencies, int TransactionPageSize,
        int DefaultRecurringApplicability, bool AutoApplyOnLoad);
}
