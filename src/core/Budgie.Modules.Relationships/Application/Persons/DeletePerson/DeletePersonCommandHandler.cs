using Budgie.Shared.Abstractions;
using Budgie.Shared.Infrastructure;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Relationships.Application.Persons.DeletePerson;

public record DeletePersonCommand(Guid Id) : IRequest<ErrorOr<Success>>;

internal sealed class DeletePersonCommandHandler(
    IDbConnectionFactory factory, ICurrentUserProvider currentUserProvider)
    : IRequestHandler<DeletePersonCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(DeletePersonCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var affected = await factory.ExecuteAsync(
            @"DELETE FROM ""Persons"" WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new { request.Id, UserId = userId }, ct);
        return affected > 0 ? Result.Success : Error.NotFound("Person.NotFound", "Person not found.");
    }
}
