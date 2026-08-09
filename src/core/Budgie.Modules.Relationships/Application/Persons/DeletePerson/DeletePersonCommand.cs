using ErrorOr;
using Mediator;

namespace Budgie.Modules.Relationships.Application.Persons.DeletePerson;

public record DeletePersonCommand(Guid Id) : IRequest<ErrorOr<Success>>;
