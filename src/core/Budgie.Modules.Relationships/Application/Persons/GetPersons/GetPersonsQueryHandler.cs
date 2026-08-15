using Budgie.Modules.Relationships.Application;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Relationships.Application.Persons.GetPersons;

public record GetPersonsQuery : IRequest<ErrorOr<List<PersonResponse>>>;

internal sealed class GetPersonsQueryHandler(
    IPersonRepository personRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetPersonsQuery, ErrorOr<List<PersonResponse>>>
{
    public async ValueTask<ErrorOr<List<PersonResponse>>> Handle(GetPersonsQuery request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        return await personRepository.GetByUserIdAsync(userId, ct);
    }
}
