using Budgie.Modules.Relationships.Infrastructure.Database;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Relationships.Application.Tags.UpdateTag;

internal sealed class UpdateTagCommandHandler(RelationshipsDbContext dbContext)
    : IRequestHandler<UpdateTagCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(UpdateTagCommand request, CancellationToken cancellationToken)
    {
        var tag = await dbContext.Tags.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
        if (tag is null) return Error.NotFound("Tag.NotFound", "Tag not found.");

        tag.Update(request.Name, request.Color);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
