using Budgie.Modules.Finances.Application;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Persons.GetPersonSummary;

public record GetPersonSummaryQuery(Guid PersonId) : IRequest<ErrorOr<PersonSummaryResponse>>;

public record PersonSummaryResponse(long TotalTransactions, decimal TotalContributed,
    decimal TotalRepaid, decimal TotalPending, string Currency);

internal sealed class GetPersonSummaryQueryHandler(ITransactionRepository txRepository)
    : IRequestHandler<GetPersonSummaryQuery, ErrorOr<PersonSummaryResponse>>
{
    public async ValueTask<ErrorOr<PersonSummaryResponse>> Handle(GetPersonSummaryQuery request, CancellationToken ct)
    {
        var summary = await txRepository.GetPersonSummaryAsync(request.PersonId, ct);
        return summary;
    }
}
