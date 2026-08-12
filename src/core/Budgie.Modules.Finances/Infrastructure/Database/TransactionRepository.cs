using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Application.Transactions;
using Budgie.Modules.Finances.Application.Transactions.CreateTransaction;
using Budgie.Modules.Finances.Application.Transactions.GetTransactions;
using Budgie.Modules.Finances.Domain.Transactions;
using Budgie.Shared.Enums;
using Budgie.Shared.Infrastructure;
using Dapper;

namespace Budgie.Modules.Finances.Infrastructure.Database;

internal sealed class TransactionRepository(IDbConnectionFactory factory) : ITransactionRepository
{
    public async Task<TransactionInfo?> GetByIdForDeleteAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var row = await factory.QuerySingleOrDefaultAsync<TxRow>(
            @"SELECT ""Id"",""Type"",""IsInitialBalance"",""Amount"",""Currency"",""ExchangeRate"",""Installments"",""AccountId"",""ParentTransactionId"",""Date"" FROM ""Transactions"" WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new { Id = id, UserId = userId }, ct);
        return row is null ? null : new TransactionInfo(row.Id, (TransactionType)row.Type, row.IsInitialBalance,
            row.Amount, row.Currency, row.ExchangeRate, row.Installments, row.AccountId, row.ParentTransactionId);
    }

    public async Task<List<TransactionInfo>> GetChildTransactionsForDeleteAsync(Guid parentId, CancellationToken ct)
        => (await factory.QueryListAsync<TxRow>(
            @"SELECT ""Id"",""Type"",""IsInitialBalance"",""Amount"",""Currency"",""ExchangeRate"",""Installments"",""AccountId"",""ParentTransactionId"",""Date"" FROM ""Transactions"" WHERE ""ParentTransactionId"" = @Id",
            new { Id = parentId }, ct))
            .Select(r => new TransactionInfo(r.Id, (TransactionType)r.Type, r.IsInitialBalance,
                r.Amount, r.Currency, r.ExchangeRate, r.Installments, r.AccountId, r.ParentTransactionId))
            .ToList();

    public async Task<Guid?> GetFirstParticipantPersonIdAsync(Guid txId, CancellationToken ct)
        => await factory.QuerySingleOrDefaultAsync<Guid?>(
            @"SELECT ""PersonId"" FROM ""TransactionParticipants"" WHERE ""TransactionId"" = @Id LIMIT 1",
            new { Id = txId }, ct);

    public Task DeleteParentParticipantAsync(Guid parentTxId, Guid personId, CancellationToken ct)
        => factory.ExecuteAsync(@"DELETE FROM ""TransactionParticipants"" WHERE ""TransactionId"" = @TxId AND ""PersonId"" = @PersonId",
            new { TxId = parentTxId, PersonId = personId }, ct);

    public Task AddAsync(Transaction tx, CancellationToken ct)
        => factory.ExecuteAsync(
            @"INSERT INTO ""Transactions"" (""Id"",""UserId"",""AccountId"",""CategoryId"",""Amount"",""Currency"",""ExchangeRate"",""Date"",""Notes"",""Type"",""Installments"",""ParentTransactionId"",""IsInitialBalance"")
              VALUES (@Id,@UserId,@AccountId,@CategoryId,@Amount,@Currency,@ExchangeRate,@Date,@Notes,@Type,@Installments,@ParentTransactionId,@IsInitialBalance)",
            new { tx.Id, tx.UserId, tx.AccountId, tx.CategoryId, Amount = tx.Amount.Amount, Currency = tx.Amount.Currency.Code, tx.ExchangeRate, tx.Date, tx.Notes, Type = (int)tx.Type, tx.Installments, tx.ParentTransactionId, tx.IsInitialBalance }, ct);

    public Task UpdateAsync(Transaction tx, CancellationToken ct)
        => factory.ExecuteAsync(
            @"UPDATE ""Transactions"" SET ""CategoryId"" = @CategoryId, ""Amount"" = @Amount, ""Currency"" = @Currency,
              ""ExchangeRate"" = @ExchangeRate, ""Date"" = @Date, ""Notes"" = @Notes, ""Type"" = @Type,
              ""Installments"" = @Installments, ""ParentTransactionId"" = @ParentTransactionId
              WHERE ""Id"" = @Id",
            new
            {
                tx.CategoryId, Amount = tx.Amount.Amount, Currency = tx.Amount.Currency.Code,
                tx.ExchangeRate, tx.Date, tx.Notes, Type = (int)tx.Type, tx.Installments,
                tx.ParentTransactionId, tx.Id
            }, ct);

    public async Task<TransactionForUpdate?> GetByIdForUpdateAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var row = await factory.QuerySingleOrDefaultAsync<TxRow>(
            @"SELECT ""Id"",""Type"",""IsInitialBalance"",""Amount"",""Currency"",""ExchangeRate"",""Installments"",""AccountId"",""ParentTransactionId"",""Date"" FROM ""Transactions"" WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new { Id = id, UserId = userId }, ct);
        return row is null ? null : new TransactionForUpdate(row.Id, (TransactionType)row.Type, row.IsInitialBalance,
            row.Amount, row.Currency, row.ExchangeRate, row.Installments, row.AccountId, row.ParentTransactionId, row.Date);
    }

    public Task AddParticipantAsync(TransactionParticipant p, CancellationToken ct)
        => factory.ExecuteAsync(
            @"INSERT INTO ""TransactionParticipants"" (""Id"",""TransactionId"",""PersonId"",""ContributedAmount"",""ContributedCurrency"",""RemainingAmount"",""RemainingCurrency"",""IsTrackingOnly"",""DestinationAccountId"")
              VALUES (@Id,@TransactionId,@PersonId,@ContributedAmount,@ContributedCurrency,@RemainingAmount,@RemainingCurrency,@IsTrackingOnly,@DestinationAccountId)",
            new { p.Id, p.TransactionId, p.PersonId, ContributedAmount = p.ContributedAmount.Amount, ContributedCurrency = p.ContributedAmount.Currency.Code, RemainingAmount = p.RemainingAmount.Amount, RemainingCurrency = p.RemainingAmount.Currency.Code, p.IsTrackingOnly, p.DestinationAccountId }, ct);

    public async Task<decimal?> GetParticipantRemainingAsync(Guid participantId, Guid userId, CancellationToken ct)
        => await factory.QuerySingleOrDefaultAsync<decimal?>(
            @"SELECT tp.""RemainingAmount"" FROM ""TransactionParticipants"" tp
              JOIN ""Transactions"" t ON t.""Id"" = tp.""TransactionId""
              WHERE tp.""Id"" = @Id AND t.""UserId"" = @UserId",
            new { Id = participantId, UserId = userId }, ct);

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

        if (request.IncludeRunningBalance && request.AccountIds is { Count: 1 })
            return await GetWithRunningBalanceAsync(request, where, p, ct);

        var pageSize = request.PageSize ?? 100;
        var offset = ((request.Page ?? 1) - 1) * pageSize;
        p.Add("PageSize", pageSize); p.Add("Offset", offset);
        var rows = await factory.QueryListAsync<TxQueryRow>(
            $@"SELECT t.""Id"",t.""AccountId"",a.""Name"" AS AccountName,t.""CategoryId"",c.""Name"" AS CategoryName,t.""Amount"",t.""Currency"",t.""ExchangeRate"",t.""Date"",t.""Notes"",t.""Type"",t.""Installments"",t.""IsInitialBalance"",t.""LinkedTransactionId"",t.""ParentTransactionId"" FROM ""Transactions"" t JOIN ""Accounts"" a ON a.""Id""=t.""AccountId"" LEFT JOIN ""Categories"" c ON c.""Id""=t.""CategoryId"" WHERE {where} ORDER BY t.""Date"" DESC, (t.""ParentTransactionId"" IS NULL), (COALESCE(t.""ParentTransactionId"", t.""Id"")), (CASE WHEN t.""Type"" = 3 AND t.""ParentTransactionId"" IS NOT NULL THEN 0 ELSE 1 END), t.""Id"" LIMIT @PageSize OFFSET @Offset", p, ct);

        var baseItems = rows.Select(r => BaseResponse(r)).ToList();
        return await BuildResponsesAsync(baseItems, ct);
    }

    private async Task<List<TransactionResponse>> GetWithRunningBalanceAsync(
        GetTransactionsQuery request, string where, DynamicParameters p, CancellationToken ct)
    {
        var accountId = request.AccountIds![0];
        var account = await factory.QuerySingleOrDefaultAsync<AccountCurrencyRow>(
            @"SELECT ""Id"", ""Currency"" FROM ""Accounts"" WHERE ""Id"" = @Id", new { Id = accountId }, ct);
        if (account is null) return new List<TransactionResponse>();

        var startDate = request.StartDate;
        var startBalance = startDate.HasValue
            ? await factory.QuerySingleAsync<decimal>(
                @"SELECT COALESCE(SUM(CASE WHEN ""Type"" IN (0,4) THEN ""Amount"" ELSE -""Amount"" END * CASE WHEN ""Currency"" = @Currency THEN 1 ELSE ""ExchangeRate"" END),0)
                  FROM ""Transactions"" WHERE ""AccountId"" = @AccountId AND ""Date"" < @StartDate",
                new { AccountId = accountId, Currency = account.Currency, StartDate = startDate.Value }, ct)
            : 0m;

        var all = await factory.QueryListAsync<TxQueryRow>(
            $@"SELECT t.""Id"",t.""AccountId"",a.""Name"" AS AccountName,t.""CategoryId"",c.""Name"" AS CategoryName,t.""Amount"",t.""Currency"",t.""ExchangeRate"",t.""Date"",t.""Notes"",t.""Type"",t.""Installments"",t.""IsInitialBalance"",t.""LinkedTransactionId"",t.""ParentTransactionId"" FROM ""Transactions"" t JOIN ""Accounts"" a ON a.""Id""=t.""AccountId"" LEFT JOIN ""Categories"" c ON c.""Id""=t.""CategoryId"" WHERE {where} ORDER BY t.""Date"" ASC, (t.""ParentTransactionId"" IS NOT NULL), (COALESCE(t.""ParentTransactionId"", t.""Id"")), (CASE WHEN t.""Type"" = 3 AND t.""ParentTransactionId"" IS NOT NULL THEN 0 ELSE 1 END), t.""Id""", p, ct);

        var runningBalance = startBalance;
        var parentRunningBalance = new Dictionary<Guid, decimal>();
        var items = new List<TransactionResponse>(all.Count);
        foreach (var row in all)
        {
            var amount = row.Currency == account.Currency ? row.Amount : row.Amount * row.ExchangeRate;
            var isVirtualLoan = row.ParentTransactionId.HasValue && row.Type == 3;
            if (!isVirtualLoan)
            {
                runningBalance += row.Type is 0 or 4 ? amount : -amount;
                if (row.ParentTransactionId is null)
                    parentRunningBalance[row.Id] = runningBalance;
            }

            var displayBalance = isVirtualLoan && row.ParentTransactionId.HasValue
                && parentRunningBalance.TryGetValue(row.ParentTransactionId.Value, out var pb)
                ? pb : runningBalance;

            items.Add(BaseResponse(row) with { RunningBalance = displayBalance });
        }

        items.Reverse();
        var pageSize = request.PageSize ?? 100;
        var page = request.Page ?? 1;
        if (items.Count > 0 && (page - 1) * pageSize > items.Count)
            page = 1;
        var slice = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return await BuildResponsesAsync(slice, ct);
    }

    private static TransactionResponse BaseResponse(TxQueryRow r) => new(
        r.Id, r.AccountId, r.AccountName, r.CategoryId, r.CategoryName,
        r.Amount, r.Currency, r.ExchangeRate, r.Date, r.Notes,
        (TransactionType)r.Type, r.Installments, r.IsInitialBalance,
        null, r.LinkedTransactionId, r.ParentTransactionId);

    private async Task<List<TransactionResponse>> BuildResponsesAsync(List<TransactionResponse> items, CancellationToken ct)
    {
        if (items.Count == 0) return items;

        var txIds = items.Select(r => r.Id).ToList();
        var participantRows = await factory.QueryListAsync<ParticipantRow>(
            @"SELECT ""TransactionId"",""PersonId"",""ContributedAmount"",""RemainingAmount"",""IsTrackingOnly"",""DestinationAccountId"" FROM ""TransactionParticipants"" WHERE ""TransactionId"" = ANY(@Ids)",
            new { Ids = txIds.ToArray() }, ct);
        var participantsByTx = participantRows.GroupBy(x => x.TransactionId)
            .ToDictionary(g => g.Key, g => g.Select(x => new TransactionParticipantDto(
                x.PersonId, x.ContributedAmount, x.RemainingAmount, x.IsTrackingOnly, x.DestinationAccountId)).ToList());

        var transferIds = items.Where(r => r.Type == TransactionType.Transfer && r.LinkedTransactionId.HasValue)
            .Select(r => r.LinkedTransactionId!.Value).Distinct().ToList();
        var destRows = new List<AccountNameRow>();
        if (transferIds.Count > 0)
        {
            destRows = (await factory.QueryListAsync<AccountNameRow>(
                @"SELECT t.""AccountId"", a.""Name"" AS AccountName, t.""LinkedTransactionId"" FROM ""Transactions"" t JOIN ""Accounts"" a ON a.""Id"" = t.""AccountId"" WHERE t.""LinkedTransactionId"" = ANY(@Ids)",
                new { Ids = transferIds.ToArray() }, ct)).ToList();
        }
        var destByLinked = destRows.GroupBy(x => x.LinkedTransactionId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        return items.Select(item =>
        {
            Guid? destId = null;
            string? destName = null;
            if (item.Type == TransactionType.Transfer && item.LinkedTransactionId.HasValue &&
                destByLinked.TryGetValue(item.LinkedTransactionId.Value, out var dest))
            {
                destId = dest.AccountId;
                destName = dest.AccountName;
            }

            return item with
            {
                TransferDestinationAccountId = destId,
                TransferDestinationAccountName = destName,
                Participants = participantsByTx.TryGetValue(item.Id, out var ps) ? ps : null
            };
        }).ToList();
    }

    public Task AddTrackingParticipant(Guid txId, Guid personId, string currency, CancellationToken ct)
        => factory.ExecuteAsync(@"INSERT INTO ""TransactionParticipants"" (""Id"",""TransactionId"",""PersonId"",""ContributedAmount"",""ContributedCurrency"",""RemainingAmount"",""RemainingCurrency"",""IsTrackingOnly"") VALUES (@Id,@TxId,@PId,0,@Cur,0,@Cur,TRUE)", new { Id = Guid.NewGuid(), TxId = txId, PId = personId, Cur = currency }, ct);

    private static (string where, DynamicParameters p) BuildWhere(GetTransactionsQuery r)
    {
        var c = new List<string>();
        var p = new DynamicParameters();
        if (r.AccountIds is { Count: > 0 }) { c.Add(@"t.""AccountId"" = ANY(@AccountIds)"); p.Add("AccountIds", r.AccountIds.ToArray()); }
        if (r.CategoryIds is { Count: > 0 }) { c.Add(@"t.""CategoryId"" = ANY(@CategoryIds)"); p.Add("CategoryIds", r.CategoryIds.ToArray()); }
        if (r.TransactionTypes is { Count: > 0 }) { c.Add(@"t.""Type"" = ANY(@Types)"); p.Add("Types", r.TransactionTypes.Select(t => (int)t).ToArray()); }
        if (r.StartDate.HasValue) { c.Add(@"t.""Date"" >= @StartDate"); p.Add("StartDate", r.StartDate.Value); }
        if (r.EndDate.HasValue) { c.Add(@"t.""Date"" < @EndDate"); p.Add("EndDate", r.EndDate.Value.AddDays(1)); }
        if (!string.IsNullOrWhiteSpace(r.Search)) { c.Add(@"(t.""Notes"" ILIKE @Search OR a.""Name"" ILIKE @Search)"); p.Add("Search", $"%{r.Search}%"); }
        if (r.PersonId.HasValue) { c.Add(@"EXISTS(SELECT 1 FROM ""TransactionParticipants"" tp WHERE tp.""TransactionId"" = t.""Id"" AND tp.""PersonId"" = @PersonId)"); p.Add("PersonId", r.PersonId.Value); }
        if (r.TagIds is { Count: > 0 }) { c.Add(@"EXISTS(SELECT 1 FROM ""TransactionParticipants"" tp JOIN ""PersonTags"" pt ON pt.""PersonId"" = tp.""PersonId"" WHERE tp.""TransactionId"" = t.""Id"" AND pt.""TagId"" = @TagId)"); p.Add("TagId", r.TagIds[0]); }
        return (c.Count > 0 ? string.Join(" AND ", c) : "TRUE", p);
    }

    private record TxRow(Guid Id, int Type, bool IsInitialBalance, decimal Amount,
        string Currency, decimal ExchangeRate, int Installments, Guid AccountId, Guid? ParentTransactionId, DateTime Date);

    private record TxQueryRow(Guid Id, Guid AccountId, string AccountName, Guid? CategoryId,
        string? CategoryName, decimal Amount, string Currency, decimal ExchangeRate,
        DateTime Date, string? Notes, int Type, int Installments, bool IsInitialBalance,
        Guid? LinkedTransactionId, Guid? ParentTransactionId);

    private record ParticipantRow(Guid TransactionId, Guid PersonId, decimal ContributedAmount,
        decimal RemainingAmount, bool IsTrackingOnly, Guid? DestinationAccountId);

    private record AccountCurrencyRow(Guid Id, string Currency);

    private record AccountNameRow(Guid AccountId, string AccountName, Guid? LinkedTransactionId);
}
