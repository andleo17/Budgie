using Budgie.Shared.Abstractions;
using Budgie.Shared.Infrastructure;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Relationships.Application.Persons.GetPersons;

public record GetPersonsQuery : IRequest<ErrorOr<List<PersonResponse>>>;

internal sealed class GetPersonsQueryHandler(
    IDbConnectionFactory factory, ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetPersonsQuery, ErrorOr<List<PersonResponse>>>
{
    public async ValueTask<ErrorOr<List<PersonResponse>>> Handle(GetPersonsQuery request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var persons = (await factory.QueryListAsync<PersonRow>(
            @"SELECT ""Id"",""Name"",""PhotoPath"" FROM ""Persons"" WHERE ""UserId"" = @UserId",
            new { UserId = userId }, ct)).ToList();

        var allTags = await factory.QueryListAsync<TagRef>(
            @"SELECT pt.""PersonId"", t.""Id"", t.""Name"", t.""Color""
              FROM ""PersonTags"" pt JOIN ""Tags"" t ON t.""Id"" = pt.""TagId""", null, ct);

        var tagsByPerson = allTags.GroupBy(t => t.PersonId)
            .ToDictionary(g => g.Key, g => g.Select(t => new TagDto(t.Id, t.Name, t.Color)).ToList());

        return persons.Select(p => new PersonResponse(p.Id, p.Name, p.PhotoPath,
            tagsByPerson.GetValueOrDefault(p.Id))).ToList();
    }

    private record PersonRow(Guid Id, string Name, string? PhotoPath);
    private record TagRef(Guid PersonId, Guid Id, string Name, string? Color);
}
