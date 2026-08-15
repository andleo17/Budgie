using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Infrastructure.Logging;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Budgie.Modules.Finances.Application.Goals.UpdateGoal;

public record UpdateGoalCommand(
    Guid Id,
    decimal TargetAmount,
    DateTime? TargetDate) : IRequest<ErrorOr<Success>>;

internal sealed class UpdateGoalCommandHandler(
    IGoalRepository goalRepository,
    IAccountRepository accountRepository,
    ICurrentUserProvider currentUserProvider,
    ILogger<UpdateGoalCommandHandler> logger)
    : IRequestHandler<UpdateGoalCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(UpdateGoalCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var goal = await goalRepository.GetByIdAsync(request.Id, userId, ct);

        if (goal is null)
            return Error.NotFound("Goal.NotFound", "Goal not found.");

        var account = await accountRepository.GetByIdAsync(goal.AccountId, userId, ct);
        if (account is null)
            return Error.NotFound("Account.NotFound", "Account not found.");

        goal.Update(request.TargetAmount, account.Balance.Currency.Code, request.TargetDate);
        await goalRepository.UpdateAsync(goal, ct);

        LogMessages.GoalUpdated(logger, request.Id);

        return Result.Success;
    }
}
