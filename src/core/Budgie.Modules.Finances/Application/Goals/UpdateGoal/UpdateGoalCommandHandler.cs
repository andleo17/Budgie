using Budgie.Modules.Finances.Domain.Goals;
using Budgie.Modules.Finances.Infrastructure.Logging;
using Budgie.Shared.Abstractions;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Infrastructure;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Goals.UpdateGoal;

public record UpdateGoalCommand(
    Guid Id,
    decimal TargetAmount,
    DateTime? TargetDate) : IRequest<ErrorOr<Success>>;

internal sealed class UpdateGoalCommandHandler(
    IDbConnectionFactory factory,
    ICurrentUserProvider currentUserProvider,
    ILogger<UpdateGoalCommandHandler> logger)
    : IRequestHandler<UpdateGoalCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(UpdateGoalCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();

        var row = await factory.QuerySingleOrDefaultAsync<GoalRow>(
            @"SELECT ""Id"",""AccountId"",""TargetCurrency"",""TargetAmount"",""TargetDate"",""CreatedAt""
              FROM ""Goals"" WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new { Id = request.Id, UserId = userId }, ct);

        if (row is null)
            return Error.NotFound("Goal.NotFound", "Goal not found.");

        var currency = await factory.QuerySingleOrDefaultAsync<string>(
            @"SELECT ""Currency"" FROM ""Accounts"" WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new { Id = row.AccountId, UserId = userId }, ct);

        if (currency is null)
            return Error.NotFound("Account.NotFound", "Account not found.");

        var goal = new Goal
        {
            Id = row.Id,
            UserId = userId,
            AccountId = row.AccountId,
            TargetAmount = Money.FromAny(row.TargetAmount, CurrencyCode.From(row.TargetCurrency)),
            TargetDate = row.TargetDate,
            CreatedAt = row.CreatedAt
        };
        goal.Update(request.TargetAmount, currency, request.TargetDate);

        await factory.ExecuteAsync(
            @"UPDATE ""Goals"" SET ""TargetAmount"" = @TargetAmount, ""TargetCurrency"" = @TargetCurrency,
               ""TargetDate"" = @TargetDate WHERE ""Id"" = @Id",
            new
            {
                TargetAmount = goal.TargetAmount.Amount,
                TargetCurrency = goal.TargetAmount.Currency.Code,
                TargetDate = goal.TargetDate,
                Id = request.Id
            }, ct);

        LogMessages.GoalUpdated(logger, request.Id);

        return Result.Success;
    }

    private record GoalRow(Guid Id, Guid AccountId, string TargetCurrency,
        decimal TargetAmount, DateTime? TargetDate, DateTime CreatedAt);
}
