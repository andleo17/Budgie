using ErrorOr;
using Mediator;

namespace Budgie.Modules.Relationships.Application.Tags.UpdateTag;

public record UpdateTagCommand(Guid Id, string Name, string? Color = null) : IRequest<ErrorOr<Success>>;
