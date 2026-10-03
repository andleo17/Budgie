using Budgie.Modules.EmailImport.Application;
using Budgie.Modules.EmailImport.Domain.Staging;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using Budgie.Shared.Infrastructure;

namespace Budgie.Modules.EmailImport.Infrastructure.Database;

internal sealed class PendingEmailTransactionRepository(IDbConnectionFactory factory) : IPendingEmailTransactionRepository
{
    // The JSONB column "FieldConfidence" is read as text (the entity stores the raw JSON string)
    // and written with a ::jsonb cast. Amount/Currency are flat columns mapped to Money/CurrencyCode.
    private const string Columns =
        @"""Id"",""UserId"",""ConnectionId"",""RuleId"",""MessageId"",""ThreadId"",""Subject"",""Merchant"",""Notes"",""Amount"",""Currency"",""ExchangeRate"",""Date"",""TransactionType"",""Confidence"",""FieldConfidence""::text AS ""FieldConfidence"",""RawBody"",""Status"",""CreatedTransactionId"",""FailureReason"",""CreatedAt"",""ReviewedAt""";

    // First idempotency barrier (spec §3.5): UNIQUE("UserId","MessageId"). ON CONFLICT DO NOTHING
    // + affected-row count instead of catching PostgresException 23505 — this path runs for
    // every message of every sync.
    public async Task<bool> TryAddAsync(PendingEmailTransaction pending, CancellationToken ct)
    {
        const string sql =
            @"INSERT INTO ""PendingEmailTransactions"" (""Id"",""UserId"",""ConnectionId"",""RuleId"",""MessageId"",""ThreadId"",""Subject"",""Merchant"",""Notes"",""Amount"",""Currency"",""ExchangeRate"",""Date"",""TransactionType"",""Confidence"",""FieldConfidence"",""RawBody"",""Status"",""CreatedTransactionId"",""FailureReason"",""CreatedAt"",""ReviewedAt"")
              VALUES (@Id,@UserId,@ConnectionId,@RuleId,@MessageId,@ThreadId,@Subject,@Merchant,@Notes,@Amount,@Currency,@ExchangeRate,@Date,@TransactionType,@Confidence,@FieldConfidence::jsonb,@RawBody,@Status,@CreatedTransactionId,@FailureReason,@CreatedAt,@ReviewedAt)
              ON CONFLICT (""UserId"",""MessageId"") DO NOTHING";
        var affected = await factory.ExecuteAsync(sql, BuildWriteParameters(pending), ct);
        return affected != 0;
    }

    public async Task<PendingEmailTransaction?> GetByIdAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var row = await factory.QuerySingleOrDefaultAsync<PendingRow>(
            $@"SELECT {Columns} FROM ""PendingEmailTransactions""
               WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new { Id = id, UserId = userId }, ct);
        return row is null ? null : Map(row);
    }

    // reviewedAt comes from the caller (the handler) because the domain never reads the clock;
    // it is relabeled Unspecified for the same Npgsql Kind=Utc reason as the other timestamps.
    public Task MarkCommittedAsync(Guid userId, Guid id, Guid transactionId, DateTime reviewedAt, CancellationToken ct)
        => factory.ExecuteAsync(
            @"UPDATE ""PendingEmailTransactions""
              SET ""Status"" = @Status, ""CreatedTransactionId"" = @TransactionId, ""ReviewedAt"" = @ReviewedAt
              WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new
            {
                Status = (int)PendingEmailStatus.Committed,
                TransactionId = transactionId,
                ReviewedAt = DateTime.SpecifyKind(reviewedAt, DateTimeKind.Unspecified),
                Id = id,
                UserId = userId
            }, ct);

    public Task MarkDismissedAsync(Guid userId, Guid id, DateTime reviewedAt, CancellationToken ct)
        => factory.ExecuteAsync(
            @"UPDATE ""PendingEmailTransactions""
              SET ""Status"" = @Status, ""ReviewedAt"" = @ReviewedAt
              WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new
            {
                Status = (int)PendingEmailStatus.Dismissed,
                ReviewedAt = DateTime.SpecifyKind(reviewedAt, DateTimeKind.Unspecified),
                Id = id,
                UserId = userId
            }, ct);

    public Task MarkFailedAsync(Guid userId, Guid id, string reason, DateTime reviewedAt, CancellationToken ct)
        => factory.ExecuteAsync(
            @"UPDATE ""PendingEmailTransactions""
              SET ""Status"" = @Status, ""FailureReason"" = @Reason, ""ReviewedAt"" = @ReviewedAt
              WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new
            {
                Status = (int)PendingEmailStatus.Failed,
                Reason = reason,
                ReviewedAt = DateTime.SpecifyKind(reviewedAt, DateTimeKind.Unspecified),
                Id = id,
                UserId = userId
            }, ct);

    // Persists user corrections. Status is written from the entity state: after ApplyReview it is
    // Pending, and committing is the approve handler's job (MarkCommitted), never a side effect.
    public Task UpdateReviewedAsync(PendingEmailTransaction pending, CancellationToken ct)
        => factory.ExecuteAsync(
            @"UPDATE ""PendingEmailTransactions""
              SET ""Amount"" = @Amount, ""Currency"" = @Currency, ""ExchangeRate"" = @ExchangeRate,
                  ""Date"" = @Date, ""Merchant"" = @Merchant, ""Notes"" = @Notes,
                  ""TransactionType"" = @TransactionType, ""Status"" = @Status
              WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new
            {
                Amount = pending.Amount.Amount,
                Currency = pending.Currency.Code,
                pending.ExchangeRate,
                Date = DateTime.SpecifyKind(pending.Date, DateTimeKind.Unspecified),
                pending.Merchant,
                pending.Notes,
                TransactionType = (int)pending.TransactionType,
                Status = (int)pending.Status,
                pending.Id,
                pending.UserId
            }, ct);

    public async Task<PagedResult<PendingEmailTransaction>> GetPendingAsync(Guid userId, int page, int pageSize, CancellationToken ct)
    {
        var total = await factory.QuerySingleAsync<int>(
            @"SELECT COUNT(*) FROM ""PendingEmailTransactions""
              WHERE ""UserId"" = @UserId AND ""Status"" = @Status",
            new { UserId = userId, Status = (int)PendingEmailStatus.Pending }, ct);

        var offset = (page - 1) * pageSize;
        var rows = await factory.QueryListAsync<PendingRow>(
            $@"SELECT {Columns} FROM ""PendingEmailTransactions""
               WHERE ""UserId"" = @UserId AND ""Status"" = @Status
               ORDER BY ""CreatedAt"" DESC
               LIMIT @PageSize OFFSET @Offset",
            new { UserId = userId, Status = (int)PendingEmailStatus.Pending, PageSize = pageSize, Offset = offset }, ct);

        return new PagedResult<PendingEmailTransaction>(rows.Select(Map).ToList(), total, page, pageSize);
    }

    public async Task<List<PendingEmailTransaction>> GetHighConfidenceAsync(Guid userId, decimal minConfidence, int take, CancellationToken ct)
    {
        var rows = await factory.QueryListAsync<PendingRow>(
            $@"SELECT {Columns} FROM ""PendingEmailTransactions""
               WHERE ""UserId"" = @UserId AND ""Status"" = @Status AND ""Confidence"" >= @MinConfidence
               ORDER BY ""Confidence"" DESC
               LIMIT @Take",
            new { UserId = userId, Status = (int)PendingEmailStatus.Pending, MinConfidence = minConfidence, Take = take }, ct);
        return rows.Select(Map).ToList();
    }

    public async Task<List<PendingEmailTransaction>> GetFailedAsync(Guid userId, CancellationToken ct)
    {
        var rows = await factory.QueryListAsync<PendingRow>(
            $@"SELECT {Columns} FROM ""PendingEmailTransactions""
               WHERE ""UserId"" = @UserId AND ""Status"" = @Status
               ORDER BY ""CreatedAt"" DESC",
            new { UserId = userId, Status = (int)PendingEmailStatus.Failed }, ct);
        return rows.Select(Map).ToList();
    }

    public Task<int> CountPendingAsync(Guid userId, CancellationToken ct)
        => factory.QuerySingleAsync<int>(
            @"SELECT COUNT(*) FROM ""PendingEmailTransactions""
              WHERE ""UserId"" = @UserId AND ""Status"" = @Status",
            new { UserId = userId, Status = (int)PendingEmailStatus.Pending }, ct);

    // Shared by INSERT and UPDATE-adjacent writes that carry the full row. Money/CurrencyCode
    // flatten to their scalar columns (Amount + Currency.Code); FieldConfidenceJson (a raw JSON
    // string in the domain) binds with a ::jsonb cast, NULL when null/empty.
    private static object BuildWriteParameters(PendingEmailTransaction pending) => new
    {
        pending.Id,
        pending.UserId,
        pending.ConnectionId,
        pending.RuleId,
        pending.MessageId,
        pending.ThreadId,
        pending.Subject,
        pending.Merchant,
        pending.Notes,
        Amount = pending.Amount.Amount,
        Currency = pending.Currency.Code,
        pending.ExchangeRate,
        Date = DateTime.SpecifyKind(pending.Date, DateTimeKind.Unspecified),
        TransactionType = (int)pending.TransactionType,
        pending.Confidence,
        FieldConfidence = string.IsNullOrWhiteSpace(pending.FieldConfidenceJson) ? null : pending.FieldConfidenceJson,
        pending.RawBody,
        Status = (int)pending.Status,
        pending.CreatedTransactionId,
        pending.FailureReason,
        CreatedAt = DateTime.SpecifyKind(pending.CreatedAt, DateTimeKind.Unspecified),
        ReviewedAt = pending.ReviewedAt is null
            ? (DateTime?)null
            : DateTime.SpecifyKind(pending.ReviewedAt.Value, DateTimeKind.Unspecified)
    };

    private static PendingEmailTransaction Map(PendingRow row) => new()
    {
        Id = row.Id,
        UserId = row.UserId,
        ConnectionId = row.ConnectionId,
        RuleId = row.RuleId,
        MessageId = row.MessageId,
        ThreadId = row.ThreadId,
        Subject = row.Subject,
        Merchant = row.Merchant,
        Notes = row.Notes,
        Amount = Money.FromPositive(row.Amount, CurrencyCode.From(row.Currency)),
        Currency = CurrencyCode.From(row.Currency),
        ExchangeRate = row.ExchangeRate,
        Date = DateTime.SpecifyKind(row.Date, DateTimeKind.Unspecified),
        TransactionType = (TransactionType)row.TransactionType,
        Confidence = row.Confidence,
        FieldConfidenceJson = row.FieldConfidence,
        RawBody = row.RawBody,
        Status = (PendingEmailStatus)row.Status,
        CreatedTransactionId = row.CreatedTransactionId,
        FailureReason = row.FailureReason,
        CreatedAt = DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Unspecified),
        ReviewedAt = row.ReviewedAt is null
            ? null
            : DateTime.SpecifyKind(row.ReviewedAt.Value, DateTimeKind.Unspecified)
    };

    // Flat row record on purpose: Amount/Currency arrive as scalars so the Money/CurrencyCode
    // mapping stays explicit instead of half-materializing the domain type via Dapper.
    private record PendingRow(Guid Id, Guid UserId, Guid ConnectionId, Guid? RuleId, string MessageId,
        string? ThreadId, string? Subject, string? Merchant, string? Notes, decimal Amount,
        string Currency, decimal ExchangeRate, DateTime Date, int TransactionType, decimal Confidence,
        string? FieldConfidence, string? RawBody, int Status, Guid? CreatedTransactionId,
        string? FailureReason, DateTime CreatedAt, DateTime? ReviewedAt);
}
