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
        var raw = await dbContext.Persons
            .AsNoTracking()
            .Include(p => p.Tags)
            .ToListAsync(cancellationToken);

        var persons = raw
            .OrderBy(p => p.Name.Value, StringComparer.CurrentCultureIgnoreCase)
            .Select(p => new PersonResponse(
                p.Id,
                p.Name,
                p.Tags.Select(t => new TagResponse(t.Id, t.Name, t.Color)).ToList(),
                p.PhotoPath != null ? "/" + p.PhotoPath : null))
            .ToList();

        return persons;
    }
}
