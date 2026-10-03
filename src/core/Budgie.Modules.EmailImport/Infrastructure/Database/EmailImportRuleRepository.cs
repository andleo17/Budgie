using Budgie.Modules.EmailImport.Application;
using Budgie.Modules.EmailImport.Domain.Rules;
using Budgie.Shared.Enums;
using Budgie.Shared.Infrastructure;

namespace Budgie.Modules.EmailImport.Infrastructure.Database;

internal sealed class EmailImportRuleRepository(IDbConnectionFactory factory) : IEmailImportRuleRepository
{
    // "CreatedAt" is intentionally absent: the domain entity does not carry it, the DDL
    // defaults it to NOW() on insert, and nothing reads it back.
    // The JSONB column "FieldPatterns" is read as text (the entity stores the raw JSON string)
    // and written with a ::jsonb cast.
    private const string Columns =
        @"""Id"",""UserId"",""Name"",""AdapterKey"",""Priority"",""IsEnabled"",""AccountId"",""CategoryId"",""TransactionType"",""Currency"",""ExchangeRate"",""FromAddress"",""SubjectContains"",""BodyRegex"",""FieldPatterns""::text AS ""FieldPatterns"",""AutoCommit""";

    public async Task<EmailImportRule?> GetByIdAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var row = await factory.QuerySingleOrDefaultAsync<RuleRow>(
            $@"SELECT {Columns} FROM ""EmailImportRules""
               WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new { Id = id, UserId = userId }, ct);
        return row is null ? null : Map(row);
    }

    public async Task<List<EmailImportRule>> GetByUserAsync(Guid userId, CancellationToken ct)
    {
        var rows = await factory.QueryListAsync<RuleRow>(
            $@"SELECT {Columns} FROM ""EmailImportRules""
               WHERE ""UserId"" = @UserId
               ORDER BY ""CreatedAt"" ASC",
            new { UserId = userId }, ct);
        return rows.Select(Map).ToList();
    }

    public Task AddAsync(EmailImportRule rule, CancellationToken ct)
        => factory.ExecuteAsync(
            @"INSERT INTO ""EmailImportRules"" (""Id"",""UserId"",""Name"",""AdapterKey"",""Priority"",""IsEnabled"",""AccountId"",""CategoryId"",""TransactionType"",""Currency"",""ExchangeRate"",""FromAddress"",""SubjectContains"",""BodyRegex"",""FieldPatterns"",""AutoCommit"")
              VALUES (@Id,@UserId,@Name,@AdapterKey,@Priority,@IsEnabled,@AccountId,@CategoryId,@TransactionType,@Currency,@ExchangeRate,@FromAddress,@SubjectContains,@BodyRegex,@FieldPatterns::jsonb,@AutoCommit)",
            BuildWriteParameters(rule), ct);

    public Task UpdateAsync(EmailImportRule rule, CancellationToken ct)
        => factory.ExecuteAsync(
            @"UPDATE ""EmailImportRules""
              SET ""Name"" = @Name, ""AdapterKey"" = @AdapterKey, ""Priority"" = @Priority,
                  ""IsEnabled"" = @IsEnabled, ""AccountId"" = @AccountId, ""CategoryId"" = @CategoryId,
                  ""TransactionType"" = @TransactionType, ""Currency"" = @Currency,
                  ""ExchangeRate"" = @ExchangeRate, ""FromAddress"" = @FromAddress,
                  ""SubjectContains"" = @SubjectContains, ""BodyRegex"" = @BodyRegex,
                  ""FieldPatterns"" = @FieldPatterns::jsonb, ""AutoCommit"" = @AutoCommit
              WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            BuildWriteParameters(rule), ct);

    public Task DeleteAsync(Guid userId, Guid id, CancellationToken ct)
        => factory.ExecuteAsync(
            @"DELETE FROM ""EmailImportRules"" WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new { Id = id, UserId = userId }, ct);

    // Shared by INSERT and UPDATE. FieldPatternsJson (a raw JSON string in the domain) is bound
    // with a ::jsonb cast; null/empty binds as NULL, matching the column's nullability.
    private static object BuildWriteParameters(EmailImportRule rule) => new
    {
        rule.Id,
        rule.UserId,
        rule.Name,
        rule.AdapterKey,
        rule.Priority,
        rule.IsEnabled,
        rule.AccountId,
        rule.CategoryId,
        TransactionType = (int)rule.TransactionType,
        rule.Currency,
        rule.ExchangeRate,
        rule.FromAddress,
        rule.SubjectContains,
        rule.BodyRegex,
        FieldPatterns = string.IsNullOrWhiteSpace(rule.FieldPatternsJson) ? null : rule.FieldPatternsJson,
        rule.AutoCommit
    };

    private static EmailImportRule Map(RuleRow row) => new()
    {
        Id = row.Id,
        UserId = row.UserId,
        Name = row.Name,
        AdapterKey = row.AdapterKey,
        Priority = row.Priority,
        IsEnabled = row.IsEnabled,
        AccountId = row.AccountId,
        CategoryId = row.CategoryId,
        TransactionType = (TransactionType)row.TransactionType,
        Currency = row.Currency,
        ExchangeRate = row.ExchangeRate,
        FromAddress = row.FromAddress,
        SubjectContains = row.SubjectContains,
        BodyRegex = row.BodyRegex,
        FieldPatternsJson = row.FieldPatterns,
        AutoCommit = row.AutoCommit
    };

    private record RuleRow(Guid Id, Guid UserId, string Name, string AdapterKey, int Priority,
        bool IsEnabled, Guid AccountId, Guid? CategoryId, int TransactionType, string Currency,
        decimal? ExchangeRate, string? FromAddress, string? SubjectContains, string? BodyRegex,
        string? FieldPatterns, bool AutoCommit);
}
