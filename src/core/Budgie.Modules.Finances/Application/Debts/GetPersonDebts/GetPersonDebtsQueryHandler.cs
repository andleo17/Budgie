using Budgie.Modules.Finances.Application;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Debts.GetPersonDebts;

public record GetPersonDebtsQuery(Guid PersonId) : IRequest<ErrorOr<List<DebtResponse>>>;

public record DebtResponse(Guid ParticipantId, Guid TransactionId, Guid PersonId,
    decimal ContributedAmount, decimal RemainingAmount, DateTime Date, string? Notes, string Currency);

internal sealed class GetPersonDebtsQueryHandler(ITransactionRepository txRepository)
    : IRequestHandler<GetPersonDebtsQuery, ErrorOr<List<DebtResponse>>>
{
    public async ValueTask<ErrorOr<List<DebtResponse>>> Handle(GetPersonDebtsQuery request, CancellationToken ct)
    {
        var debts = await txRepository.GetPersonDebtsAsync(request.PersonId, ct);
        return debts;
    }
}
