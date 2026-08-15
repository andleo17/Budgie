using Budgie.Modules.Relationships.Application;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Relationships.Application.Tags.DeleteTag;

public record DeleteTagCommand(Guid Id) : IRequest<ErrorOr<Success>>;

internal sealed class DeleteTagCommandHandler(
    ITagRepository tagRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<DeleteTagCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(DeleteTagCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var deleted = await tagRepository.DeleteAsync(request.Id, userId, ct);
        return deleted ? Result.Success : Error.NotFound("Tag.NotFound", "Tag not found.");
    }
}
