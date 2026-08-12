using Budgie.Shared.Abstractions;
using Budgie.Shared.Infrastructure;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Goals.GetGoals;

public record GetGoalsQuery() : IRequest<ErrorOr<List<GoalResponse>>>;

internal sealed class GetGoalsQueryHandler(
    IDbConnectionFactory factory,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetGoalsQuery, ErrorOr<List<GoalResponse>>>
{
    public async ValueTask<ErrorOr<List<GoalResponse>>> Handle(GetGoalsQuery request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();

        var rows = await factory.QueryListAsync<GoalRow>(
            @"SELECT g.""Id"", g.""AccountId"", a.""Name"" AS AccountName, a.""Currency"" AS Currency,
                    a.""Balance"" AS CurrentBalance, g.""TargetAmount"", g.""TargetDate""
              FROM ""Goals"" g
              INNER JOIN ""Accounts"" a ON a.""Id"" = g.""AccountId""
              WHERE g.""UserId"" = @UserId",
            new { UserId = userId }, ct);

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

    private record GoalRow(
        Guid Id, Guid AccountId, string AccountName, string Currency,
        decimal CurrentBalance, decimal TargetAmount, DateTime? TargetDate);
}
