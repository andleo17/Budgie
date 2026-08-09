using ErrorOr;
using Mediator;

namespace Budgie.Modules.Relationships.Application.Persons.UpdatePerson;

public record UpdatePersonCommand(Guid Id, string Name, List<string>? TagNames = null) : IRequest<ErrorOr<Success>>;
