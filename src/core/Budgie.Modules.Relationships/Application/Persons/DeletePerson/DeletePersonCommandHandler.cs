using Budgie.Modules.Relationships.Application;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Relationships.Application.Persons.DeletePerson;

public record DeletePersonCommand(Guid Id) : IRequest<ErrorOr<Success>>;

internal sealed class DeletePersonCommandHandler(
    IPersonRepository personRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<DeletePersonCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(DeletePersonCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var deleted = await personRepository.DeleteAsync(request.Id, userId, ct);
        return deleted ? Result.Success : Error.NotFound("Person.NotFound", "Person not found.");
    }
}
