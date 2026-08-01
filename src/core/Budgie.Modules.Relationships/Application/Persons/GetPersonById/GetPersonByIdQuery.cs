using ErrorOr;
using MediatR;

namespace Budgie.Modules.Relationships.Application.Persons.GetPersonById;

public record GetPersonByIdQuery(Guid Id) : IRequest<ErrorOr<PersonResponse>>;
