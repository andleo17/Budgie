using Budgie.Modules.Relationships.Application;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Relationships.Application.Persons.UpdatePerson;

public record UpdatePersonCommand(Guid Id, string Name, List<string>? TagNames) : IRequest<ErrorOr<Success>>;

internal sealed class UpdatePersonCommandHandler(
    IPersonRepository personRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<UpdatePersonCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(UpdatePersonCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var updated = await personRepository.UpdateAsync(request.Id, userId, request.Name, request.TagNames, ct);
        return updated ? Result.Success : Error.NotFound("Person.NotFound", "Person not found.");
    }
}
