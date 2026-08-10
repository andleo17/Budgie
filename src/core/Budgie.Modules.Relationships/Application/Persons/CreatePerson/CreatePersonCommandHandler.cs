using Budgie.Shared.Abstractions;
using Budgie.Shared.Infrastructure;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Relationships.Application.Persons.CreatePerson;

public record CreatePersonCommand(string Name, List<string>? TagNames) : IRequest<ErrorOr<Guid>>;

internal sealed class CreatePersonCommandHandler(
    IDbConnectionFactory factory, ICurrentUserProvider currentUserProvider)
    : IRequestHandler<CreatePersonCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(CreatePersonCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var personId = Guid.NewGuid();
        await factory.ExecuteAsync(
            @"INSERT INTO ""Persons"" (""Id"",""UserId"",""Name"",""CreatedAt"") VALUES (@Id,@UserId,@Name,NOW())",
            new { Id = personId, UserId = userId, request.Name }, ct);

        if (request.TagNames is { Count: > 0 })
        {
            foreach (var tagName in request.TagNames)
            {
                var tag = await factory.QuerySingleOrDefaultAsync<(Guid Id, string, string?)>(
                    @"SELECT ""Id"",""Name"",""Color"" FROM ""Tags"" WHERE ""UserId"" = @UserId AND ""Name"" = @Name",
                    new { UserId = userId, Name = tagName }, ct);
                if (tag.Id != default)
                    await factory.ExecuteAsync(
                        @"INSERT INTO ""PersonTags"" (""PersonId"",""TagId"") VALUES (@PId,@TId) ON CONFLICT DO NOTHING",
                        new { PId = personId, TId = tag.Id }, ct);
            }
        }
        return personId;
    }
}
