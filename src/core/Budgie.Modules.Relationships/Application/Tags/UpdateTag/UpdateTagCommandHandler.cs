using Budgie.Shared.Abstractions;
using Budgie.Shared.Infrastructure;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Relationships.Application.Tags.UpdateTag;

public record UpdateTagCommand(Guid Id, string Name, string? Color) : IRequest<ErrorOr<Success>>;

internal sealed class UpdateTagCommandHandler(
    IDbConnectionFactory factory, ICurrentUserProvider currentUserProvider)
    : IRequestHandler<UpdateTagCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(UpdateTagCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var affected = await factory.ExecuteAsync(
            @"UPDATE ""Tags"" SET ""Name"" = @Name, ""Color"" = @Color WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new { request.Id, request.Name, request.Color, UserId = userId }, ct);
        if (affected == 0) return Error.NotFound("Tag.NotFound", "Tag not found.");
        return Result.Success;
    }
}
