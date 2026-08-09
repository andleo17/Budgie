using ErrorOr;
using Mediator;

namespace Budgie.Modules.Relationships.Application.Tags.DeleteTag;

public record DeleteTagCommand(Guid Id) : IRequest<ErrorOr<Success>>;
