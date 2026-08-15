using Budgie.Modules.Relationships.Application;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Relationships.Application.Tags.UpdateTag;

public record UpdateTagCommand(Guid Id, string Name, string? Color) : IRequest<ErrorOr<Success>>;

internal sealed class UpdateTagCommandHandler(
    ITagRepository tagRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<UpdateTagCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(UpdateTagCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var updated = await tagRepository.UpdateAsync(request.Id, userId, request.Name, request.Color, ct);
        return updated ? Result.Success : Error.NotFound("Tag.NotFound", "Tag not found.");
    }
}
