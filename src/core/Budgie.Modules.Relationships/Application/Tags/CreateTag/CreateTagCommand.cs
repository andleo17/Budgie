using ErrorOr;
using Mediator;

namespace Budgie.Modules.Relationships.Application.Tags.CreateTag;

public record CreateTagCommand(string Name, string? Color = null) : IRequest<ErrorOr<Guid>>;
