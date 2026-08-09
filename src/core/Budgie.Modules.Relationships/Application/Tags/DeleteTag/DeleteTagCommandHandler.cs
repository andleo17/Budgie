using Budgie.Modules.Relationships.Infrastructure.Database;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Relationships.Application.Tags.DeleteTag;

internal sealed class DeleteTagCommandHandler(RelationshipsDbContext dbContext)
    : IRequestHandler<DeleteTagCommand, ErrorOr<Success>>
{
    public async ValueTask<ErrorOr<Success>> Handle(DeleteTagCommand request, CancellationToken cancellationToken)
    {
        var tag = await dbContext.Tags.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
        if (tag is null) return Error.NotFound("Tag.NotFound", "Tag not found.");

        dbContext.Tags.Remove(tag);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
