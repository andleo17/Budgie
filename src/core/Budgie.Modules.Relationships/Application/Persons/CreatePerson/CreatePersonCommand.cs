using ErrorOr;
using Mediator;

namespace Budgie.Modules.Relationships.Application.Persons.CreatePerson;

public record CreatePersonCommand(string Name, List<string>? TagNames = null) : IRequest<ErrorOr<Guid>>;
