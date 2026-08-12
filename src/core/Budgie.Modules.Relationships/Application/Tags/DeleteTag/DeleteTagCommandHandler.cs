using Budgie.Shared.Abstractions;
using Budgie.Shared.Infrastructure;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Relationships.Application.Tags.DeleteTag;

public record DeleteTagCommand(Guid Id) : IRequest<ErrorOr<Success>>;

internal sealed class DeleteTagCommandHandler(
    IDbConnectionFactory factory, ICurrentUserProvider currentUserProvider)
    : IRequestHandler<DeleteTagCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(DeleteTagCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var affected = await factory.ExecuteAsync(
            @"DELETE FROM ""Tags"" WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new { request.Id, UserId = userId }, ct);
        if (affected == 0) return Error.NotFound("Tag.NotFound", "Tag not found.");
        return Result.Success;
    }
}
