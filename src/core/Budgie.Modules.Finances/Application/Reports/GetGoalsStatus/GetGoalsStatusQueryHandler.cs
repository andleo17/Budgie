using Budgie.Modules.Finances.Application;
using Budgie.Shared.Abstractions;
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
    IReportRepository reportRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetGoalsStatusQuery, ErrorOr<List<GoalStatus>>>
{
    public async ValueTask<ErrorOr<List<GoalStatus>>> Handle(GetGoalsStatusQuery request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var statusList = await reportRepository.GetGoalsStatusAsync(userId, ct);
        return statusList;
    }
}
