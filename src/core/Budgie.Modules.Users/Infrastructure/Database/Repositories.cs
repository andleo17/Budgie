using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Budgie.Modules.Users.Domain;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
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

internal sealed class UserRepository(IDbConnectionFactory factory) : Application.IUserRepository
{
    public Task<bool> AnyAsync(CancellationToken ct)
        => factory.QuerySingleAsync<bool>(
            @"SELECT EXISTS(SELECT 1 FROM ""Users"")",
            null, ct);

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct)
        => factory.QuerySingleAsync<bool>(
            @"SELECT EXISTS(SELECT 1 FROM ""Users"" WHERE ""Email"" = @Email)",
            new { Email = email }, ct);

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct)
    {
        var row = await factory.QuerySingleOrDefaultAsync<UserRow>(
            @"SELECT ""Id"",""Email"",""PasswordHash"",""Name"",COALESCE(""Role"", 2) AS ""Role"" FROM ""Users"" WHERE ""Email"" = @Email",
            new { Email = email }, ct);
        return row is null ? null : Map(row);
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var row = await factory.QuerySingleOrDefaultAsync<UserRow>(
            @"SELECT ""Id"",""Email"",""PasswordHash"",""Name"",COALESCE(""Role"", 2) AS ""Role"" FROM ""Users"" WHERE ""Id"" = @Id",
            new { Id = id }, ct);
        return row is null ? null : Map(row);
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct)
        => factory.QuerySingleAsync<bool>(
            @"SELECT EXISTS(SELECT 1 FROM ""Users"" WHERE ""Id"" = @Id)",
            new { Id = id }, ct);

    public async Task AddAsync(User user, CancellationToken ct)
        => await factory.ExecuteAsync(
            @"INSERT INTO ""Users"" (""Id"",""Email"",""PasswordHash"",""Name"",""Role"",""CreatedAt"")
              VALUES (@Id,@Email,@PasswordHash,@Name,@Role,NOW())",
            new { user.Id, Email = user.Email.Value, user.PasswordHash, Name = user.Name.Value, Role = (int)user.Role }, ct);

    public async Task UpdateAsync(User user, CancellationToken ct)
        => await factory.ExecuteAsync(
            @"UPDATE ""Users"" SET ""Name"" = @Name, ""Role"" = @Role WHERE ""Id"" = @Id",
            new { user.Id, Name = user.Name.Value, Role = (int)user.Role }, ct);

    private static User Map(UserRow row) => new()
    {
        Id = row.Id,
        Email = EmailAddress.From(row.Email),
        PasswordHash = row.PasswordHash,
        Name = Name.From(row.Name),
        Role = (UserRole)row.Role
    };

    private record UserRow(Guid Id, string Email, string PasswordHash, string Name, int Role);
}

internal sealed class RefreshTokenRepository(IDbConnectionFactory factory) : Application.IRefreshTokenRepository
{
    public async Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct)
    {
        var row = await factory.QuerySingleOrDefaultAsync<TokenRow>(
            @"SELECT ""Id"",""UserId"",""TokenHash"",""ExpiresAt"",""CreatedAt"",""IsRevoked""
              FROM ""RefreshTokens"" WHERE ""TokenHash"" = @TokenHash",
            new { TokenHash = tokenHash }, ct);
        return row is null ? null : new RefreshToken
        {
            Id = row.Id, UserId = row.UserId, TokenHash = row.TokenHash,
            ExpiresAt = row.ExpiresAt, CreatedAt = row.CreatedAt, IsRevoked = row.IsRevoked
        };
    }

    public async Task AddAsync(RefreshToken token, CancellationToken ct)
        => await factory.ExecuteAsync(
            @"INSERT INTO ""RefreshTokens"" (""Id"",""UserId"",""TokenHash"",""ExpiresAt"",""CreatedAt"",""IsRevoked"")
              VALUES (@Id,@UserId,@TokenHash,@ExpiresAt,@CreatedAt,@IsRevoked)",
            token, ct);

    public async Task RevokeAsync(Guid id, CancellationToken ct)
        => await factory.ExecuteAsync(
            @"UPDATE ""RefreshTokens"" SET ""IsRevoked"" = TRUE WHERE ""Id"" = @Id",
            new { Id = id }, ct);

    private record TokenRow(Guid Id, Guid UserId, string TokenHash, DateTime ExpiresAt, DateTime CreatedAt, bool IsRevoked);
}

internal sealed class UserInviteRepository(IDbConnectionFactory factory) : Application.IUserInviteRepository
{
    public async Task<UserInvite?> GetByHashAsync(string tokenHash, CancellationToken ct)
    {
        var row = await factory.QuerySingleOrDefaultAsync<InviteRow>(
            @"SELECT ""Id"",""TokenHash"",""Email"",""ExpiresAt"",""CreatedAt"",""IsUsed"",""CreatedByUserId""
              FROM ""UserInvites"" WHERE ""TokenHash"" = @TokenHash",
            new { TokenHash = tokenHash }, ct);
        return row is null ? null : Map(row);
    }

    public async Task AddAsync(UserInvite invite, CancellationToken ct)
        => await factory.ExecuteAsync(
            @"INSERT INTO ""UserInvites"" (""Id"",""TokenHash"",""Email"",""ExpiresAt"",""CreatedAt"",""IsUsed"",""CreatedByUserId"")
              VALUES (@Id,@TokenHash,@Email,@ExpiresAt,@CreatedAt,@IsUsed,@CreatedByUserId)",
            new
            {
                invite.Id,
                invite.TokenHash,
                Email = invite.Email?.Value,
                invite.ExpiresAt,
                invite.CreatedAt,
                invite.IsUsed,
                invite.CreatedByUserId
            }, ct);

    public async Task MarkAsUsedAsync(Guid id, CancellationToken ct)
        => await factory.ExecuteAsync(
            @"UPDATE ""UserInvites"" SET ""IsUsed"" = TRUE WHERE ""Id"" = @Id",
            new { Id = id }, ct);

    public async Task<List<UserInvite>> GetActiveInvitesAsync(CancellationToken ct)
    {
        var rows = await factory.QueryListAsync<InviteRow>(
            @"SELECT ""Id"",""TokenHash"",""Email"",""ExpiresAt"",""CreatedAt"",""IsUsed"",""CreatedByUserId""
              FROM ""UserInvites"" WHERE NOT ""IsUsed"" AND ""ExpiresAt"" > NOW() ORDER BY ""CreatedAt"" DESC",
            null, ct);
        return rows.Select(Map).ToList();
    }

    private static UserInvite Map(InviteRow row) => new()
    {
        Id = row.Id,
        TokenHash = row.TokenHash,
        Email = string.IsNullOrWhiteSpace(row.Email) ? null : EmailAddress.From(row.Email),
        ExpiresAt = row.ExpiresAt,
        CreatedAt = row.CreatedAt,
        IsUsed = row.IsUsed,
        CreatedByUserId = row.CreatedByUserId
    };

    private record InviteRow(Guid Id, string TokenHash, string? Email, DateTime ExpiresAt, DateTime CreatedAt, bool IsUsed, Guid CreatedByUserId);
}
