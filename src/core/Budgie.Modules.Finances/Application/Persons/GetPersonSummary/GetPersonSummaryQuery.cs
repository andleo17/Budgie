using ErrorOr;
using MediatR;

namespace Budgie.Modules.Finances.Application.Persons.GetPersonSummary;

public record GetPersonSummaryQuery(Guid PersonId) : IRequest<ErrorOr<PersonSummaryResponse>>;

public record PersonSummaryResponse(
    int TotalTransactions,
    decimal TotalContributed,
    decimal TotalRepaid,
    decimal TotalPending,
    string Currency);
