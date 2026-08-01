using ErrorOr;
using MediatR;

namespace Budgie.Modules.Relationships.Application.Tags.GetTags;

public record GetTagsQuery() : IRequest<ErrorOr<List<TagResponse>>>;
