using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Application.Transactions;
using Budgie.Modules.Finances.Application.Transactions.GetTransactions;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Shared.Enums;
using Budgie.Shared.Infrastructure;
using Dapper;

namespace Budgie.Modules.Finances.Infrastructure.Database;

internal sealed class TransactionRepository(IDbConnectionFactory factory) : ITransactionRepository
{
    public async Task<TransactionInfo?> GetByIdForDeleteAsync(Guid id, CancellationToken ct)
    {
        var row = await factory.QuerySingleOrDefaultAsync<TxRow>(
            @"SELECT ""Id"",""Type"",""IsInitialBalance"",""Amount"",""AccountId"" FROM ""Transactions"" WHERE ""Id"" = @Id",
            new { Id = id }, ct);
        return row is null ? null : new TransactionInfo(row.Id, (TransactionType)row.Type, row.IsInitialBalance, row.Amount, row.AccountId);
    }

    public Task AddAsync(Transaction tx, CancellationToken ct)
        => factory.ExecuteAsync(
            @"INSERT INTO ""Transactions"" (""Id"",""UserId"",""AccountId"",""CategoryId"",""Amount"",""Currency"",""ExchangeRate"",""Date"",""Notes"",""Type"",""Installments"",""ParentTransactionId"",""IsInitialBalance"")
              VALUES (@Id,@UserId,@AccountId,@CategoryId,@Amount,@Currency,@ExchangeRate,@Date,@Notes,@Type,@Installments,@ParentTransactionId,@IsInitialBalance)",
            new { tx.Id, tx.UserId, tx.AccountId, tx.CategoryId, Amount = tx.Amount.Amount, Currency = tx.Amount.Currency.Code, tx.ExchangeRate, tx.Date, tx.Notes, Type = (int)tx.Type, tx.Installments, tx.ParentTransactionId, tx.IsInitialBalance }, ct);

    public Task AddParticipantAsync(TransactionParticipant p, CancellationToken ct)
        => factory.ExecuteAsync(
            @"INSERT INTO ""TransactionParticipants"" (""Id"",""TransactionId"",""PersonId"",""ContributedAmount"",""ContributedCurrency"",""RemainingAmount"",""RemainingCurrency"",""IsTrackingOnly"",""DestinationAccountId"")
              VALUES (@Id,@TransactionId,@PersonId,@ContributedAmount,@ContributedCurrency,@RemainingAmount,@RemainingCurrency,@IsTrackingOnly,@DestinationAccountId)",
            new { p.Id, p.TransactionId, p.PersonId, ContributedAmount = p.ContributedAmount.Amount, ContributedCurrency = p.ContributedAmount.Currency.Code, RemainingAmount = p.RemainingAmount.Amount, RemainingCurrency = p.RemainingAmount.Currency.Code, p.IsTrackingOnly, p.DestinationAccountId }, ct);

    public Task ReduceParticipantRemainingAsync(Guid participantId, decimal amount, CancellationToken ct)
        => factory.ExecuteAsync(@"UPDATE ""TransactionParticipants"" SET ""RemainingAmount"" = GREATEST(0, ""RemainingAmount"" - @Amount) WHERE ""Id"" = @Id", new { Amount = amount, Id = participantId }, ct);

    public async Task ReduceParentParticipantRemainingAsync(Guid childParticipantId, decimal amount, CancellationToken ct)
    {
        var (pTxId, personId) = await GetParentInfo(childParticipantId, ct);
        if (pTxId.HasValue)
            await factory.ExecuteAsync(@"UPDATE ""TransactionParticipants"" SET ""RemainingAmount"" = GREATEST(0, ""RemainingAmount"" - @Amount) WHERE ""TransactionId"" = @TxId AND ""PersonId"" = @PersonId AND ""RemainingAmount"" > 0", new { Amount = amount, TxId = pTxId.Value, PersonId = personId }, ct);
    }

    public Task RestoreParticipantRemainingAsync(Guid participantId, decimal amount, CancellationToken ct)
        => factory.ExecuteAsync(@"UPDATE ""TransactionParticipants"" SET ""RemainingAmount"" = ""RemainingAmount"" + @Amount WHERE ""Id"" = @Id", new { Amount = amount, Id = participantId }, ct);

    public async Task RestoreParentParticipantRemainingAsync(Guid childParticipantId, decimal amount, CancellationToken ct)
    {
        var (pTxId, personId) = await GetParentInfo(childParticipantId, ct);
        if (pTxId.HasValue)
            await factory.ExecuteAsync(@"UPDATE ""TransactionParticipants"" SET ""RemainingAmount"" = ""RemainingAmount"" + @Amount WHERE ""TransactionId"" = @TxId AND ""PersonId"" = @PersonId", new { Amount = amount, TxId = pTxId.Value, PersonId = personId }, ct);
    }

    private async Task<(Guid? parentTxId, Guid personId)> GetParentInfo(Guid participantId, CancellationToken ct)
    {
        var row = await factory.QuerySingleOrDefaultAsync<(Guid? parentTxId, Guid personId)>(
            @"SELECT t.""ParentTransactionId"", tp.""PersonId"" FROM ""TransactionParticipants"" tp JOIN ""Transactions"" t ON t.""Id"" = tp.""TransactionId"" WHERE tp.""Id"" = @Id",
            new { Id = participantId }, ct);
        return row;
    }

    public Task AddRepaymentDetailAsync(Guid repaymentTxId, Guid participantId, decimal amount, CancellationToken ct)
        => factory.ExecuteAsync(@"INSERT INTO ""RepaymentDetails"" (""RepaymentTransactionId"",""RepaidParticipantId"",""Amount"") VALUES (@RtId,@PId,@Amt)", new { RtId = repaymentTxId, PId = participantId, Amt = amount }, ct);

    public async Task<List<RepaymentDetailInfo>> GetRepaymentDetailsAsync(Guid repaymentTxId, CancellationToken ct)
        => (await factory.QueryListAsync<RepaymentDetailInfo>(@"SELECT ""RepaidParticipantId"", ""Amount"" FROM ""RepaymentDetails"" WHERE ""RepaymentTransactionId"" = @Id", new { Id = repaymentTxId }, ct)).ToList();

    public Task DeleteRepaymentDetailsAsync(Guid repaymentTxId, CancellationToken ct)
        => factory.ExecuteAsync(@"DELETE FROM ""RepaymentDetails"" WHERE ""RepaymentTransactionId"" = @Id", new { Id = repaymentTxId }, ct);

    public Task DeleteTransactionAsync(Guid id, CancellationToken ct)
        => factory.ExecuteAsync(@"DELETE FROM ""Transactions"" WHERE ""Id"" = @Id", new { Id = id }, ct);

    public Task DeleteParticipantAsync(Guid txId, CancellationToken ct)
        => factory.ExecuteAsync(@"DELETE FROM ""TransactionParticipants"" WHERE ""TransactionId"" = @Id", new { Id = txId }, ct);

    public Task DeleteChildParticipantsAsync(Guid parentTxId, CancellationToken ct)
        => factory.ExecuteAsync(@"DELETE FROM ""TransactionParticipants"" WHERE ""TransactionId"" IN (SELECT ""Id"" FROM ""Transactions"" WHERE ""ParentTransactionId"" = @Id)", new { Id = parentTxId }, ct);

    public Task DeleteChildTransactionsAsync(Guid parentTxId, CancellationToken ct)
        => factory.ExecuteAsync(@"DELETE FROM ""Transactions"" WHERE ""ParentTransactionId"" = @Id", new { Id = parentTxId }, ct);

    public async Task<int> GetTransactionCountAsync(GetTransactionsQuery request, CancellationToken ct)
    {
        var (where, p) = BuildWhere(request);
        return await factory.QuerySingleAsync<int>($@"SELECT COUNT(*) FROM ""Transactions"" t JOIN ""Accounts"" a ON a.""Id"" = t.""AccountId"" WHERE {where}", p, ct);
    }

    public async Task<List<TransactionResponse>> GetTransactionsAsync(GetTransactionsQuery request, CancellationToken ct)
    {
        var (where, p) = BuildWhere(request);
        var pageSize = request.PageSize ?? 100;
        var offset = ((request.Page ?? 1) - 1) * pageSize;
        p.Add("PageSize", pageSize); p.Add("Offset", offset);
        return (await factory.QueryListAsync<TransactionResponse>(
            $@"SELECT t.""Id"",t.""AccountId"",a.""Name"" AS AccountName,t.""CategoryId"",c.""Name"" AS CategoryName,t.""Amount"",t.""Currency"",t.""ExchangeRate"",t.""Date"",t.""Notes"",t.""Type"",t.""Installments"",t.""IsInitialBalance"",t.""LinkedTransactionId"",t.""ParentTransactionId"" FROM ""Transactions"" t JOIN ""Accounts"" a ON a.""Id""=t.""AccountId"" LEFT JOIN ""Categories"" c ON c.""Id""=t.""CategoryId"" WHERE {where} ORDER BY t.""Date"" DESC LIMIT @PageSize OFFSET @Offset", p, ct)).ToList();
    }

    public Task AddTrackingParticipant(Guid txId, Guid personId, string currency, CancellationToken ct)
        => factory.ExecuteAsync(@"INSERT INTO ""TransactionParticipants"" (""Id"",""TransactionId"",""PersonId"",""ContributedAmount"",""ContributedCurrency"",""RemainingAmount"",""RemainingCurrency"",""IsTrackingOnly"") VALUES (@Id,@TxId,@PId,0,@Cur,0,@Cur,TRUE)", new { Id = Guid.NewGuid(), TxId = txId, PId = personId, Cur = currency }, ct);

    private static (string where, DynamicParameters p) BuildWhere(GetTransactionsQuery r)
    {
        var c = new List<string> { @"t.""ParentTransactionId"" IS NULL" };
        var p = new DynamicParameters();
        if (r.AccountIds is { Count: > 0 }) { c.Add(@"t.""AccountId"" = ANY(@AccountIds)"); p.Add("AccountIds", r.AccountIds.ToArray()); }
        if (r.TransactionTypes is { Count: > 0 }) { c.Add(@"t.""Type"" = ANY(@Types)"); p.Add("Types", r.TransactionTypes.Select(t => (int)t).ToArray()); }
        if (r.StartDate.HasValue) { c.Add(@"t.""Date"" >= @StartDate"); p.Add("StartDate", r.StartDate.Value); }
        if (r.EndDate.HasValue) { c.Add(@"t.""Date"" < @EndDate"); p.Add("EndDate", r.EndDate.Value.AddDays(1)); }
        if (!string.IsNullOrWhiteSpace(r.Search)) { c.Add(@"(t.""Notes"" ILIKE @Search OR a.""Name"" ILIKE @Search)"); p.Add("Search", $"%{r.Search}%"); }
        if (r.PersonId.HasValue) { c.Add(@"EXISTS(SELECT 1 FROM ""TransactionParticipants"" tp WHERE tp.""TransactionId"" = t.""Id"" AND tp.""PersonId"" = @PersonId)"); p.Add("PersonId", r.PersonId.Value); }
        return (string.Join(" AND ", c), p);
    }

    private record TxRow(Guid Id, int Type, bool IsInitialBalance, decimal Amount, Guid AccountId);
}
