using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Domain.Goals;
using Budgie.Modules.Finances.Infrastructure.Logging;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Budgie.Modules.Finances.Application.Goals.CreateGoal;

public record CreateGoalCommand(
    Guid AccountId,
    decimal TargetAmount,
    DateTime? TargetDate) : IRequest<ErrorOr<Guid>>;

internal sealed class CreateGoalCommandHandler(
    IGoalRepository goalRepository,
    IAccountRepository accountRepository,
    ICurrentUserProvider currentUserProvider,
    ILogger<CreateGoalCommandHandler> logger)
    : IRequestHandler<CreateGoalCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(CreateGoalCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var account = await accountRepository.GetByIdAsync(request.AccountId, userId, ct);

        if (account is null)
            return Error.NotFound("Account.NotFound", "Account not found.");

        var goal = Goal.Create(userId, request.AccountId, request.TargetAmount, account.Balance.Currency.Code, request.TargetDate);
        await goalRepository.AddAsync(goal, ct);

        LogMessages.GoalCreated(logger, goal.Id);

        return goal.Id;
    }
}
