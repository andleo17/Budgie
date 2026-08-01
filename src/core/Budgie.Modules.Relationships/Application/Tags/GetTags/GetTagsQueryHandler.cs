using Budgie.Modules.Relationships.Infrastructure.Database;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Relationships.Application.Tags.GetTags;

internal sealed class GetTagsQueryHandler(RelationshipsDbContext dbContext)
    : IRequestHandler<GetTagsQuery, ErrorOr<List<TagResponse>>>
{
    public async Task<ErrorOr<List<TagResponse>>> Handle(GetTagsQuery request, CancellationToken cancellationToken)
    {
        var tags = await dbContext.Tags
            .Select(t => new TagResponse(t.Id, t.Name, t.Color))
            .ToListAsync(cancellationToken);

        return tags;
    }
}
