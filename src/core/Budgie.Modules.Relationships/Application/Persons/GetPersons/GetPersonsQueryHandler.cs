using Budgie.Modules.Relationships.Application.Tags;
using Budgie.Modules.Relationships.Infrastructure.Database;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Relationships.Application.Persons.GetPersons;

internal sealed class GetPersonsQueryHandler(RelationshipsDbContext dbContext)
    : IRequestHandler<GetPersonsQuery, ErrorOr<List<PersonResponse>>>
{
    public async Task<ErrorOr<List<PersonResponse>>> Handle(GetPersonsQuery request, CancellationToken cancellationToken)
    {
        var persons = await dbContext.Persons
            .Include(p => p.Tags)
            .OrderBy(p => p.Name)
            .Select(p => new PersonResponse(
                p.Id,
                p.Name,
                p.Tags.Select(t => new TagResponse(t.Id, t.Name, t.Color)).ToList(),
                p.PhotoPath != null ? "/" + p.PhotoPath : null))
            .ToListAsync(cancellationToken);

        return persons;
    }
}
