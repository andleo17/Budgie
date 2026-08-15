using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Application.Goals;
using Budgie.Modules.Finances.Domain.Goals;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Infrastructure;

namespace Budgie.Modules.Finances.Infrastructure.Database;

internal sealed class GoalRepository(IDbConnectionFactory factory) : IGoalRepository
{
    public async Task<Goal?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var row = await factory.QuerySingleOrDefaultAsync<GoalRow>(
            @"SELECT ""Id"",""UserId"",""AccountId"",""TargetAmount"",""TargetCurrency"",""TargetDate"",""CreatedAt""
              FROM ""Goals"" WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new { Id = id, UserId = userId }, ct);
        return row is null ? null : Map(row);
    }

    public async Task<List<GoalResponse>> GetGoalsByUserIdAsync(Guid userId, CancellationToken ct)
    {
        const string sql = @"
            SELECT g.""Id"", g.""AccountId"", a.""Name"" AS AccountName, a.""Currency"" AS Currency,
                a.""Balance"" AS CurrentBalance, g.""TargetAmount"", g.""TargetDate""
            FROM ""Goals"" g
            INNER JOIN ""Accounts"" a ON a.""Id"" = g.""AccountId""
            WHERE g.""UserId"" = @UserId
            ORDER BY g.""CreatedAt"" DESC";

        var rows = await factory.QueryListAsync<GoalListRow>(sql, new { UserId = userId }, ct);
        return rows.Select(r =>
        {
            var targetAmount = r.TargetAmount > 0 ? r.TargetAmount : 1m;
            return new GoalResponse(
                r.Id,
                r.AccountId,
                r.AccountName,
                r.Currency,
                r.CurrentBalance,
                r.TargetAmount,
                r.TargetDate,
                (double)(r.CurrentBalance / targetAmount * 100));
        }).ToList();
    }

    public Task AddAsync(Goal goal, CancellationToken ct)
        => factory.ExecuteAsync(
            @"INSERT INTO ""Goals"" (""Id"",""UserId"",""AccountId"",""TargetAmount"",""TargetCurrency"",""TargetDate"",""CreatedAt"")
              VALUES (@Id,@UserId,@AccountId,@TargetAmount,@TargetCurrency,@TargetDate,@CreatedAt)",
            new
            {
                goal.Id,
                goal.UserId,
                goal.AccountId,
                TargetAmount = goal.TargetAmount.Amount,
                TargetCurrency = goal.TargetAmount.Currency.Code,
                goal.TargetDate,
                goal.CreatedAt
            }, ct);

    public Task UpdateAsync(Goal goal, CancellationToken ct)
        => factory.ExecuteAsync(
            @"UPDATE ""Goals"" SET ""AccountId"" = @AccountId, ""TargetAmount"" = @TargetAmount,
              ""TargetCurrency"" = @TargetCurrency, ""TargetDate"" = @TargetDate
              WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new
            {
                goal.AccountId,
                TargetAmount = goal.TargetAmount.Amount,
                TargetCurrency = goal.TargetAmount.Currency.Code,
                goal.TargetDate,
                goal.Id,
                goal.UserId
            }, ct);

    public Task DeleteAsync(Guid id, Guid userId, CancellationToken ct)
        => factory.ExecuteAsync(
            @"DELETE FROM ""Goals"" WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new { Id = id, UserId = userId }, ct);

    private static Goal Map(GoalRow row) => new()
    {
        Id = row.Id,
        UserId = row.UserId,
        AccountId = row.AccountId,
        TargetAmount = Money.FromNonNegative(row.TargetAmount, CurrencyCode.From(row.TargetCurrency)),
        TargetDate = row.TargetDate,
        CreatedAt = row.CreatedAt
    };

    private record GoalRow(Guid Id, Guid UserId, Guid AccountId, decimal TargetAmount,
        string TargetCurrency, DateTime? TargetDate, DateTime CreatedAt);

    private record GoalListRow(Guid Id, Guid AccountId, string AccountName, string Currency,
        decimal CurrentBalance, decimal TargetAmount, DateTime? TargetDate);
}
