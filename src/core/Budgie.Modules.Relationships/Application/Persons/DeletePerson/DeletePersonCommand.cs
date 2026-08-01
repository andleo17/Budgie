using ErrorOr;
using MediatR;

namespace Budgie.Modules.Relationships.Application.Persons.DeletePerson;

public record DeletePersonCommand(Guid Id) : IRequest<ErrorOr<Success>>;
