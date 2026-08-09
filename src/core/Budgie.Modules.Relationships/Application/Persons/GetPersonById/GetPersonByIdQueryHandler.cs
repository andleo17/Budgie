using Budgie.Modules.Relationships.Application.Tags;
using Budgie.Modules.Relationships.Infrastructure.Database;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Relationships.Application.Persons.GetPersonById;

internal sealed class GetPersonByIdQueryHandler(RelationshipsDbContext dbContext)
    : IRequestHandler<GetPersonByIdQuery, ErrorOr<PersonResponse>>
{
    public async ValueTask<ErrorOr<PersonResponse>> Handle(GetPersonByIdQuery request, CancellationToken cancellationToken)
    {
        var person = await dbContext.Persons
            .Include(p => p.Tags)
            .Where(p => p.Id == request.Id)
            .Select(p => new PersonResponse(
                p.Id,
                p.Name,
                p.Tags.Select(t => new TagResponse(t.Id, t.Name, t.Color)).ToList(),
                p.PhotoPath != null ? "/" + p.PhotoPath : null))
            .FirstOrDefaultAsync(cancellationToken);

        if (person is null)
            return Error.NotFound("Person.NotFound", "Person not found.");

        return person;
    }
}
