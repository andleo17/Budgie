using ErrorOr;
using MediatR;

namespace Budgie.Modules.Relationships.Application.Persons.GetPersons;

public record GetPersonsQuery() : IRequest<ErrorOr<List<PersonResponse>>>;
