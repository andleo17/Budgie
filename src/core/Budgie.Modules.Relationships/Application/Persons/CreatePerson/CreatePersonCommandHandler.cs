using Budgie.Modules.Relationships.Application;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Relationships.Application.Persons.CreatePerson;

public record CreatePersonCommand(string Name, List<string>? TagNames) : IRequest<ErrorOr<Guid>>;

internal sealed class CreatePersonCommandHandler(
    IPersonRepository personRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<CreatePersonCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(CreatePersonCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var personId = Guid.NewGuid();
        await personRepository.AddAsync(personId, userId, request.Name, request.TagNames, ct);
        return personId;
    }
}
