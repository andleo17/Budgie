using Budgie.Shared.Abstractions;
using Budgie.Shared.Infrastructure;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Relationships.Application.Persons.UpdatePerson;

public record UpdatePersonCommand(Guid Id, string Name, List<string>? TagNames) : IRequest<ErrorOr<Success>>;

internal sealed class UpdatePersonCommandHandler(
    IDbConnectionFactory factory, ICurrentUserProvider currentUserProvider)
    : IRequestHandler<UpdatePersonCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(UpdatePersonCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var affected = await factory.ExecuteAsync(
            @"UPDATE ""Persons"" SET ""Name"" = @Name WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new { request.Id, request.Name, UserId = userId }, ct);
        if (affected == 0) return Error.NotFound("Person.NotFound", "Person not found.");

        if (request.TagNames is not null)
        {
            await factory.ExecuteAsync(@"DELETE FROM ""PersonTags"" WHERE ""PersonId"" = @Id", new { request.Id }, ct);
            foreach (var tagName in request.TagNames)
            {
                var tag = await factory.QuerySingleOrDefaultAsync<(Guid Id, string, string?)>(
                    @"SELECT ""Id"",""Name"",""Color"" FROM ""Tags"" WHERE ""UserId"" = @UserId AND ""Name"" = @Name",
                    new { UserId = userId, Name = tagName }, ct);
                if (tag.Id != default)
                    await factory.ExecuteAsync(
                        @"INSERT INTO ""PersonTags"" (""PersonId"",""TagId"") VALUES (@PId,@TId)",
                        new { PId = request.Id, TId = tag.Id }, ct);
            }
        }
        return Result.Success;
    }
}
