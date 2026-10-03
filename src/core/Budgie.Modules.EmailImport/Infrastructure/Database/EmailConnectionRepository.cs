using Budgie.Modules.EmailImport.Application;
using Budgie.Modules.EmailImport.Domain.Connections;
using Budgie.Shared.Enums;
using Budgie.Shared.Infrastructure;

namespace Budgie.Modules.EmailImport.Infrastructure.Database;

internal sealed class EmailConnectionRepository(IDbConnectionFactory factory) : IEmailConnectionRepository
{
    // "CreatedAt" is intentionally absent: the domain entity does not carry it, the DDL
    // defaults it to NOW() on insert, and nothing reads it back.
    private const string Columns =
        @"""Id"",""UserId"",""Provider"",""Email"",""EncryptedCredentials"",""SyncCursor"",""SyncEnabled"",""AutoCommitMinConfidence"",""LastSyncAt"",""LastError"",""LastSkippedCount""";

    public async Task<EmailConnection?> GetByIdAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var row = await factory.QuerySingleOrDefaultAsync<ConnectionRow>(
            $@"SELECT {Columns} FROM ""EmailImportConnections""
               WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new { Id = id, UserId = userId }, ct);
        return row is null ? null : Map(row);
    }

    // v1 ships one mailbox per provider per user (CompleteConnectionCommand rejects a second
    // one with EmailImport.AlreadyConnected), but the DDL constraint is
    // UNIQUE("UserId","Provider","Email") — uniqueness on the whole triple, not on the pair.
    // A second mailbox would therefore be insertable and make QuerySingleOrDefault throw with
    // more than one row. LIMIT 1 plus an explicit ORDER BY keeps this total even if that check
    // is ever bypassed or the constraint relaxed; callers that need "is there one?" should use
    // ExistsByProviderAsync, which cannot throw.
    public async Task<EmailConnection?> GetByProviderAsync(Guid userId, EmailProvider provider, CancellationToken ct)
    {
        var row = await factory.QuerySingleOrDefaultAsync<ConnectionRow>(
            $@"SELECT {Columns} FROM ""EmailImportConnections""
               WHERE ""UserId"" = @UserId AND ""Provider"" = @Provider
               ORDER BY ""CreatedAt"" ASC, ""Id"" ASC
               LIMIT 1",
            new { UserId = userId, Provider = (int)provider }, ct);
        return row is null ? null : Map(row);
    }

    /// <summary>
    /// Whether the user already has a mailbox for <paramref name="provider"/>. Cannot throw on
    /// multiple rows, unlike <see cref="GetByProviderAsync"/>.
    /// </summary>
    public async Task<bool> ExistsByProviderAsync(Guid userId, EmailProvider provider, CancellationToken ct)
        => await factory.QuerySingleAsync<bool>(
            @"SELECT EXISTS(SELECT 1 FROM ""EmailImportConnections""
                            WHERE ""UserId"" = @UserId AND ""Provider"" = @Provider)",
            new { UserId = userId, Provider = (int)provider }, ct);

    public async Task<List<EmailConnection>> GetAllAsync(Guid userId, CancellationToken ct)
    {
        var rows = await factory.QueryListAsync<ConnectionRow>(
            $@"SELECT {Columns} FROM ""EmailImportConnections""
               WHERE ""UserId"" = @UserId
               ORDER BY ""CreatedAt"" DESC",
            new { UserId = userId }, ct);
        return rows.Select(Map).ToList();
    }

    public async Task<List<EmailConnection>> GetEnabledAsync(CancellationToken ct)
    {
        // Deliberate exception to the tenancy-in-signature rule: no userId parameter.
        // Consumed by the EmailSyncWorker (BackgroundService), which has no HttpContext.
        // Each row carries its own UserId and every staging row written from it is persisted
        // under that user.
        var rows = await factory.QueryListAsync<ConnectionRow>(
            $@"SELECT {Columns} FROM ""EmailImportConnections""
               WHERE ""SyncEnabled"" = TRUE",
            null, ct);
        return rows.Select(Map).ToList();
    }

    public Task AddAsync(EmailConnection connection, CancellationToken ct)
        => factory.ExecuteAsync(
            @"INSERT INTO ""EmailImportConnections"" (""Id"",""UserId"",""Provider"",""Email"",""EncryptedCredentials"",""SyncCursor"",""SyncEnabled"",""AutoCommitMinConfidence"",""LastSyncAt"",""LastError"",""LastSkippedCount"")
              VALUES (@Id,@UserId,@Provider,@Email,@EncryptedCredentials,@SyncCursor,@SyncEnabled,@AutoCommitMinConfidence,@LastSyncAt,@LastError,@LastSkippedCount)",
            new
            {
                connection.Id,
                connection.UserId,
                Provider = (int)connection.Provider,
                connection.Email,
                connection.EncryptedCredentials,
                connection.SyncCursor,
                connection.SyncEnabled,
                connection.AutoCommitMinConfidence,
                LastSyncAt = AsUnspecified(connection.LastSyncAt),
                connection.LastError,
                connection.LastSkippedCount
            }, ct);

    // Updates every mutable column. "LastError" and "SyncCursor" are written even when NULL:
    // EmailConnection.RecordSuccess clears LastError and AdvanceCursor(null) resets the cursor.
    // "Provider" is excluded on purpose: no domain behavior changes it and it is part of
    // UNIQUE("UserId","Provider","Email"). Tenancy doubles up in the WHERE as defense in depth.
    public Task UpdateAsync(EmailConnection connection, CancellationToken ct)
        => factory.ExecuteAsync(
            @"UPDATE ""EmailImportConnections""
              SET ""Email"" = @Email, ""EncryptedCredentials"" = @EncryptedCredentials,
                  ""SyncCursor"" = @SyncCursor, ""SyncEnabled"" = @SyncEnabled,
                  ""AutoCommitMinConfidence"" = @AutoCommitMinConfidence, ""LastSyncAt"" = @LastSyncAt,
                  ""LastError"" = @LastError, ""LastSkippedCount"" = @LastSkippedCount
              WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new
            {
                connection.Email,
                connection.EncryptedCredentials,
                connection.SyncCursor,
                connection.SyncEnabled,
                connection.AutoCommitMinConfidence,
                LastSyncAt = AsUnspecified(connection.LastSyncAt),
                connection.LastError,
                connection.LastSkippedCount,
                connection.Id,
                connection.UserId
            }, ct);

    public Task DeleteAsync(Guid userId, Guid id, CancellationToken ct)
        => factory.ExecuteAsync(
            @"DELETE FROM ""EmailImportConnections"" WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new { Id = id, UserId = userId }, ct);

    private static EmailConnection Map(ConnectionRow row) => new()
    {
        Id = row.Id,
        UserId = row.UserId,
        Provider = (EmailProvider)row.Provider,
        Email = row.Email,
        EncryptedCredentials = row.EncryptedCredentials,
        SyncCursor = row.SyncCursor,
        SyncEnabled = row.SyncEnabled,
        AutoCommitMinConfidence = row.AutoCommitMinConfidence,
        LastSyncAt = AsUnspecified(row.LastSyncAt),
        LastError = row.LastError,
        LastSkippedCount = row.LastSkippedCount
    };

    // Npgsql rejects DateTime with Kind=Utc for TIMESTAMP WITHOUT TIME ZONE columns; relabel
    // (no conversion) to the wall-clock Unspecified the schema stores, like Guard.NormalizeDate.
    private static DateTime AsUnspecified(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

    private static DateTime? AsUnspecified(DateTime? value) =>
        value is null ? null : DateTime.SpecifyKind(value.Value, DateTimeKind.Unspecified);

    private record ConnectionRow(Guid Id, Guid UserId, int Provider, string Email,
        string EncryptedCredentials, string? SyncCursor, bool SyncEnabled,
        decimal AutoCommitMinConfidence, DateTime? LastSyncAt, string? LastError, int LastSkippedCount);
}
