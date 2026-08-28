using System.Data;
using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Application.RecurringTransactions.Shared;
using Budgie.Modules.Finances.Domain.RecurringTransactions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Enums;
using Budgie.Shared.Infrastructure;
using Dapper;

namespace Budgie.Modules.Finances.Infrastructure.Database;

internal sealed class RecurringTransactionRepository(IDbConnectionFactory factory) : IRecurringTransactionRepository
{
    public async Task<RecurringTransaction?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var row = await factory.QuerySingleOrDefaultAsync<RecurringTransactionRow>(
            @"SELECT r.""Id"",r.""UserId"",r.""Name"",r.""AccountId"",r.""CategoryId"",r.""Amount"",r.""Currency"",
                    r.""ExchangeRate"",r.""Notes"",r.""Type"",r.""Installments"",r.""IsInterestFree"",r.""Applicability"",
                    r.""Frequency"",r.""CustomIntervalDays"",r.""EndType"",r.""EndAfterCount"",r.""EndDate"",r.""StartDate"",
                    r.""NextDueDate"",r.""TimesApplied"",r.""LastAppliedDate"",r.""IsActive"",r.""ExternalUid""
              FROM ""RecurringTransactions"" r WHERE r.""Id"" = @Id AND r.""UserId"" = @UserId",
            new { Id = id, UserId = userId }, ct);
        if (row is null) return null;

        var template = Map(row);
        await LoadParticipantsAsync(template, ct);
        return template;
    }

    public async Task<Dictionary<string, Guid>> GetExternalUidMapAsync(Guid userId, CancellationToken ct)
    {
        var rows = await factory.QueryListAsync<ExternalUidRow>(
            @"SELECT ""ExternalUid"", ""Id"" FROM ""RecurringTransactions"" WHERE ""UserId"" = @UserId AND ""ExternalUid"" IS NOT NULL",
            new { UserId = userId }, ct);
        return rows.Where(r => r.ExternalUid is not null).ToDictionary(r => r.ExternalUid!, r => r.Id);
    }

    public async Task<List<RecurringTransaction>> GetByUserIdAsync(Guid userId, RecurringApplicability? applicability, bool? isActive, CancellationToken ct)
    {
        var conditions = new List<string> { @"r.""UserId"" = @UserId" };
        var parameters = new DynamicParameters();
        parameters.Add("UserId", userId);
        if (applicability.HasValue) { conditions.Add(@"r.""Applicability"" = @Applicability"); parameters.Add("Applicability", (int)applicability.Value); }
        if (isActive.HasValue) { conditions.Add(@"r.""IsActive"" = @IsActive"); parameters.Add("IsActive", isActive.Value); }

        var where = string.Join(" AND ", conditions);
        var rows = await factory.QueryListAsync<RecurringTransactionRow>(
            $@"SELECT r.""Id"",r.""UserId"",r.""Name"",r.""AccountId"",r.""CategoryId"",r.""Amount"",r.""Currency"",
                    r.""ExchangeRate"",r.""Notes"",r.""Type"",r.""Installments"",r.""IsInterestFree"",r.""Applicability"",
                    r.""Frequency"",r.""CustomIntervalDays"",r.""EndType"",r.""EndAfterCount"",r.""EndDate"",r.""StartDate"",
                    r.""NextDueDate"",r.""TimesApplied"",r.""LastAppliedDate"",r.""IsActive"",r.""ExternalUid""
              FROM ""RecurringTransactions"" r WHERE {where}
              ORDER BY COALESCE(r.""LastAppliedDate"", r.""StartDate"", '0001-01-01') DESC",
            parameters, ct);

        var templates = rows.Select(Map).ToList();
        await LoadParticipantsAsync(templates, ct);
        return templates;
    }

    public async Task<List<RecurringTransaction>> GetDueByUserIdAsync(Guid userId, CancellationToken ct)
    {
        var rows = await factory.QueryListAsync<RecurringTransactionRow>(
            @"SELECT r.""Id"",r.""UserId"",r.""Name"",r.""AccountId"",r.""CategoryId"",r.""Amount"",r.""Currency"",
                    r.""ExchangeRate"",r.""Notes"",r.""Type"",r.""Installments"",r.""IsInterestFree"",r.""Applicability"",
                    r.""Frequency"",r.""CustomIntervalDays"",r.""EndType"",r.""EndAfterCount"",r.""EndDate"",r.""StartDate"",
                    r.""NextDueDate"",r.""TimesApplied"",r.""LastAppliedDate"",r.""IsActive"",r.""ExternalUid""
              FROM ""RecurringTransactions"" r
              WHERE r.""UserId"" = @UserId AND r.""IsActive"" = TRUE
                AND r.""Applicability"" IN (1, 2)
                AND r.""NextDueDate"" IS NOT NULL AND r.""NextDueDate"" <= @Now
              ORDER BY r.""NextDueDate""",
            new { UserId = userId, Now = DateTime.UtcNow }, ct);

        var templates = rows.Select(Map).ToList();
        await LoadParticipantsAsync(templates, ct);
        return templates;
    }

    public async Task<List<DueRecurringResponse>> GetDueResponsesAsync(Guid userId, CancellationToken ct)
        => (await factory.QueryListAsync<DueRecurringResponse>(
            @"SELECT ""Id"",""Name"",""AccountId"",""Amount"",""Currency"",""Type""
              FROM ""RecurringTransactions""
              WHERE ""UserId"" = @UserId AND ""IsActive"" = TRUE
                AND ""NextDueDate"" IS NOT NULL AND ""NextDueDate"" <= @Now
              ORDER BY ""NextDueDate""",
            new { UserId = userId, Now = DateTime.UtcNow }, ct)).ToList();

    public Task AddAsync(RecurringTransaction template, CancellationToken ct, IDbTransaction? tx = null)
    {
        const string sql =
            @"INSERT INTO ""RecurringTransactions"" (""Id"",""UserId"",""AccountId"",""CategoryId"",""Name"",""Amount"",""Currency"",""ExchangeRate"",""Notes"",""Type"",""Installments"",""IsInterestFree"",""Applicability"",""Frequency"",""CustomIntervalDays"",""EndType"",""EndAfterCount"",""EndDate"",""StartDate"",""NextDueDate"",""TimesApplied"",""LastAppliedDate"",""IsActive"",""ExternalUid"")
              VALUES (@Id,@UserId,@AccountId,@CategoryId,@Name,@Amount,@Currency,@ExchangeRate,@Notes,@Type,@Installments,@IsInterestFree,@Applicability,@Frequency,@CustomIntervalDays,@EndType,@EndAfterCount,@EndDate,@StartDate,@NextDueDate,@TimesApplied,@LastAppliedDate,@IsActive,@ExternalUid)";
        var p = new
        {
            template.Id, template.UserId, template.AccountId, template.CategoryId,
            Name = template.Name.Value, Amount = template.Amount.Amount,
            Currency = template.Amount.Currency.Code, template.ExchangeRate, template.Notes,
            Type = (int)template.Type, template.Installments, template.IsInterestFree,
            Applicability = (int)template.Applicability,
            Frequency = (int?)template.Frequency ?? 3,
            template.CustomIntervalDays, EndType = template.EndType.HasValue ? (int?)template.EndType.Value : null,
            template.EndAfterCount, template.EndDate, template.StartDate,
            template.NextDueDate, template.TimesApplied, template.LastAppliedDate, template.IsActive,
            template.ExternalUid
        };
        return tx is not null
            ? tx.Connection!.ExecuteAsync(new CommandDefinition(sql, p, transaction: tx, cancellationToken: ct))
            : factory.ExecuteAsync(sql, p, ct);
    }

    public Task AddParticipantsAsync(RecurringTransaction template, CancellationToken ct)
        => InsertParticipantsAsync(template.Id, template.Participants, ct);

    public Task UpdateAsync(RecurringTransaction template, CancellationToken ct)
        => factory.ExecuteAsync(
            @"UPDATE ""RecurringTransactions"" SET ""Name"" = @Name, ""AccountId"" = @AccountId, ""CategoryId"" = @CategoryId,
              ""Amount"" = @Amount, ""Currency"" = @Currency, ""ExchangeRate"" = @ExchangeRate, ""Notes"" = @Notes,
              ""Type"" = @Type, ""Installments"" = @Installments, ""IsInterestFree"" = @IsInterestFree,
              ""Applicability"" = @Applicability, ""Frequency"" = @Frequency, ""CustomIntervalDays"" = @CustomIntervalDays,
              ""EndType"" = @EndType, ""EndAfterCount"" = @EndAfterCount, ""EndDate"" = @EndDate, ""StartDate"" = @StartDate,
              ""NextDueDate"" = @NextDueDate
              WHERE ""Id"" = @Id",
            new
            {
                Name = template.Name.Value, template.AccountId, template.CategoryId,
                Amount = template.Amount.Amount, Currency = template.Amount.Currency.Code,
                template.ExchangeRate, template.Notes, Type = (int)template.Type,
                template.Installments, template.IsInterestFree,
                Applicability = (int)template.Applicability,
                Frequency = (int?)template.Frequency ?? 3,
                template.CustomIntervalDays,
                EndType = template.EndType.HasValue ? (int?)template.EndType.Value : null,
                template.EndAfterCount, template.EndDate, template.StartDate, template.NextDueDate, template.Id
            }, ct);

    public Task ReplaceParticipantsAsync(RecurringTransaction template, CancellationToken ct)
        => InsertParticipantsAsync(template.Id, template.Participants, ct, clearFirst: true);

    public Task UpdateApplyStateAsync(RecurringTransaction template, CancellationToken ct)
        => factory.ExecuteAsync(
            @"UPDATE ""RecurringTransactions"" SET ""NextDueDate"" = @NextDueDate, ""TimesApplied"" = @TimesApplied,
              ""LastAppliedDate"" = @LastAppliedDate, ""IsActive"" = @IsActive WHERE ""Id"" = @Id",
            new { template.NextDueDate, template.TimesApplied, template.LastAppliedDate, template.IsActive, template.Id }, ct);

    public Task DeleteAsync(Guid id, Guid userId, CancellationToken ct)
        => factory.ExecuteAsync(@"DELETE FROM ""RecurringTransactions"" WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new { Id = id, UserId = userId }, ct);

    private async Task InsertParticipantsAsync(Guid templateId, IReadOnlyCollection<RecurringTransactionParticipant> participants, CancellationToken ct, bool clearFirst = false)
    {
        if (participants.Count == 0) return;

        if (clearFirst)
            await factory.ExecuteAsync(@"DELETE FROM ""RecurringTransactionParticipants"" WHERE ""RecurringTransactionId"" = @Id", new { Id = templateId }, ct);

        foreach (var p in participants)
        {
            await factory.ExecuteAsync(
                @"INSERT INTO ""RecurringTransactionParticipants"" (""Id"",""RecurringTransactionId"",""PersonId"",""ContributedAmount"",""ContributedCurrency"",""RemainingAmount"",""RemainingCurrency"",""IsTrackingOnly"",""DestinationAccountId"")
                  VALUES (@Id,@RecurringTransactionId,@PersonId,@ContributedAmount,@ContributedCurrency,@RemainingAmount,@RemainingCurrency,@IsTrackingOnly,@DestinationAccountId)",
                new
                {
                    p.Id, p.RecurringTransactionId, p.PersonId,
                    ContributedAmount = p.ContributedAmount.Amount,
                    ContributedCurrency = p.ContributedAmount.Currency.Code,
                    RemainingAmount = p.RemainingAmount.Amount,
                    RemainingCurrency = p.RemainingAmount.Currency.Code,
                    p.IsTrackingOnly, p.DestinationAccountId
                }, ct);
        }
    }

    private static RecurringTransaction Map(RecurringTransactionRow row)
    {
        var isTemplate = row.Applicability == (int)RecurringApplicability.Template;
        return new RecurringTransaction
        {
            Id = row.Id,
            UserId = row.UserId,
            Name = Name.From(row.Name, 100),
            AccountId = row.AccountId,
            CategoryId = row.CategoryId,
            Amount = Money.FromAny(row.Amount, CurrencyCode.From(row.Currency)),
            ExchangeRate = row.ExchangeRate,
            Notes = row.Notes,
            Type = (TransactionType)row.Type,
            Installments = row.Installments,
            IsInterestFree = row.IsInterestFree,
            Applicability = (RecurringApplicability)row.Applicability,
            Frequency = isTemplate ? null : (RecurringFrequency?)row.Frequency,
            CustomIntervalDays = isTemplate ? null : row.CustomIntervalDays,
            EndType = isTemplate ? null : row.EndType.HasValue ? (RecurringEndType?)row.EndType.Value : null,
            EndAfterCount = isTemplate ? null : row.EndAfterCount,
            EndDate = isTemplate ? null : row.EndDate,
            StartDate = isTemplate ? null : row.StartDate,
            NextDueDate = isTemplate ? null : row.NextDueDate,
            TimesApplied = row.TimesApplied,
            LastAppliedDate = row.LastAppliedDate,
            IsActive = row.IsActive,
            ExternalUid = row.ExternalUid
        };
    }

    private async Task LoadParticipantsAsync(RecurringTransaction template, CancellationToken ct)
    {
        var rows = await factory.QueryListAsync<RecurringTransactionParticipantRow>(
            @"SELECT ""RecurringTransactionId"",""PersonId"",""ContributedAmount"",""ContributedCurrency"",""RemainingAmount"",""RemainingCurrency"",""IsTrackingOnly"",""DestinationAccountId""
              FROM ""RecurringTransactionParticipants"" WHERE ""RecurringTransactionId"" = @Id",
            new { Id = template.Id }, ct);
        foreach (var row in rows)
            template.CreateParticipant(row.PersonId, row.ContributedAmount, row.RemainingAmount, row.IsTrackingOnly, row.DestinationAccountId);
    }

    private async Task LoadParticipantsAsync(List<RecurringTransaction> templates, CancellationToken ct)
    {
        if (templates.Count == 0) return;

        var ids = templates.Select(t => t.Id).ToArray();
        var rows = await factory.QueryListAsync<RecurringTransactionParticipantRow>(
            @"SELECT ""RecurringTransactionId"",""PersonId"",""ContributedAmount"",""ContributedCurrency"",""RemainingAmount"",""RemainingCurrency"",""IsTrackingOnly"",""DestinationAccountId""
              FROM ""RecurringTransactionParticipants"" WHERE ""RecurringTransactionId"" = ANY(@Ids)",
            new { Ids = ids }, ct);

        var byTemplate = rows.GroupBy(r => r.RecurringTransactionId)
            .ToDictionary(g => g.Key, g => g.ToList());
        foreach (var template in templates)
        {
            if (!byTemplate.TryGetValue(template.Id, out var participantRows)) continue;
            foreach (var row in participantRows)
                template.CreateParticipant(row.PersonId, row.ContributedAmount, row.RemainingAmount, row.IsTrackingOnly, row.DestinationAccountId);
        }
    }

    private record RecurringTransactionRow(Guid Id, Guid UserId, string Name, Guid AccountId, Guid? CategoryId,
        decimal Amount, string Currency, decimal ExchangeRate, string? Notes, int Type, int Installments,
        bool IsInterestFree, int Applicability, int Frequency, int? CustomIntervalDays, int? EndType,
        int? EndAfterCount, DateTime? EndDate, DateTime? StartDate, DateTime? NextDueDate,
        int TimesApplied, DateTime? LastAppliedDate, bool IsActive, string? ExternalUid);

    private record ExternalUidRow(string? ExternalUid, Guid Id);

    private record RecurringTransactionParticipantRow(Guid RecurringTransactionId, Guid PersonId,
        decimal ContributedAmount, string ContributedCurrency, decimal RemainingAmount,
        string RemainingCurrency, bool IsTrackingOnly, Guid? DestinationAccountId);
}
