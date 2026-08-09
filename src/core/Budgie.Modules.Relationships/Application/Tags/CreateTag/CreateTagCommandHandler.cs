using Budgie.Modules.Relationships.Domain.Tags;
using Budgie.Modules.Relationships.Infrastructure.Database;
using Budgie.Shared.Abstractions;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Relationships.Application.Tags.CreateTag;

internal sealed class CreateTagCommandHandler(
    RelationshipsDbContext dbContext,
    ICurrentUserProvider currentUserProvider)
    : IRequestHandler<CreateTagCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(CreateTagCommand request, CancellationToken cancellationToken)
    {
        var tag = Tag.Create(currentUserProvider.GetUserId(), request.Name, request.Color);
        dbContext.Tags.Add(tag);
        await dbContext.SaveChangesAsync(cancellationToken);
        return tag.Id;
    }
}
