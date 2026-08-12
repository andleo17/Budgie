using Budgie.Modules.Finances.Domain.Goals;
using Budgie.Modules.Finances.Infrastructure.Logging;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Infrastructure;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Goals.CreateGoal;

public record CreateGoalCommand(
    Guid AccountId,
    decimal TargetAmount,
    DateTime? TargetDate) : IRequest<ErrorOr<Guid>>;

internal sealed class CreateGoalCommandHandler(
    IDbConnectionFactory factory,
    ICurrentUserProvider currentUserProvider,
    ILogger<CreateGoalCommandHandler> logger)
    : IRequestHandler<CreateGoalCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(CreateGoalCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();

        var currency = await factory.QuerySingleOrDefaultAsync<string>(
            @"SELECT ""Currency"" FROM ""Accounts"" WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new { Id = request.AccountId, UserId = userId }, ct);

        if (currency is null)
            return Error.NotFound("Account.NotFound", "Account not found.");

        var goal = Goal.Create(userId, request.AccountId, request.TargetAmount, currency, request.TargetDate);

        await factory.ExecuteAsync(
            @"INSERT INTO ""Goals"" (""Id"",""UserId"",""AccountId"",""TargetAmount"",""TargetCurrency"",""TargetDate"",""CreatedAt"")
              VALUES (@Id,@UserId,@AccountId,@TargetAmount,@TargetCurrency,@TargetDate,@CreatedAt)",
            new
            {
                goal.Id, goal.UserId, goal.AccountId,
                TargetAmount = goal.TargetAmount.Amount,
                TargetCurrency = goal.TargetAmount.Currency.Code,
                goal.TargetDate, goal.CreatedAt
            }, ct);

        LogMessages.GoalCreated(logger, goal.Id);

        return goal.Id;
    }
}
