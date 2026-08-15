using Budgie.Modules.Relationships.Application;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Relationships.Application.Tags;

public record TagResponse(Guid Id, string Name, string? Color);

public record CreateTagCommand(string Name, string? Color) : IRequest<ErrorOr<Guid>>;

internal sealed class CreateTagCommandHandler(
    ITagRepository tagRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<CreateTagCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(CreateTagCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        return await tagRepository.AddAsync(userId, request.Name, request.Color, ct);
    }
}

public record GetTagsQuery : IRequest<ErrorOr<List<TagResponse>>>;

internal sealed class GetTagsQueryHandler(
    ITagRepository tagRepository,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetTagsQuery, ErrorOr<List<TagResponse>>>
{
    public async ValueTask<ErrorOr<List<TagResponse>>> Handle(GetTagsQuery request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        return await tagRepository.GetByUserIdAsync(userId, ct);
    }
}
