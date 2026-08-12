using Budgie.Shared.Abstractions;
using Budgie.Shared.Infrastructure;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Reports.GetGoalsStatus;

public record GetGoalsStatusQuery() : IRequest<ErrorOr<List<GoalStatus>>>;

public record GoalStatus(
    Guid AccountId,
    string AccountName,
    decimal CurrentBalance,
    decimal TargetAmount,
    double ProgressPercentage,
    DateTime? TargetDate);

internal sealed class GetGoalsStatusQueryHandler(
    IDbConnectionFactory factory,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetGoalsStatusQuery, ErrorOr<List<GoalStatus>>>
{
    public async ValueTask<ErrorOr<List<GoalStatus>>> Handle(GetGoalsStatusQuery request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();

        var rows = await factory.QueryListAsync<GoalStatusRow>(
            @"SELECT a.""Id"" AS AccountId, a.""Name"" AS AccountName, a.""Balance"" AS CurrentBalance,
                    g.""TargetAmount"", g.""TargetDate""
              FROM ""Goals"" g
              INNER JOIN ""Accounts"" a ON a.""Id"" = g.""AccountId""
              WHERE g.""UserId"" = @UserId AND g.""TargetAmount"" > 0",
            new { UserId = userId }, ct);

        return rows.Select(r => new GoalStatus(
            r.AccountId,
            r.AccountName,
            r.CurrentBalance,
            r.TargetAmount,
            (double)(r.CurrentBalance / r.TargetAmount * 100),
            r.TargetDate)).ToList();
    }

    private record GoalStatusRow(Guid AccountId, string AccountName, decimal CurrentBalance,
        decimal TargetAmount, DateTime? TargetDate);
}
