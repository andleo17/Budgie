using ErrorOr;
using MediatR;

namespace Budgie.Modules.Relationships.Application.Tags.UpdateTag;

public record UpdateTagCommand(Guid Id, string Name, string? Color = null) : IRequest<ErrorOr<Success>>;
