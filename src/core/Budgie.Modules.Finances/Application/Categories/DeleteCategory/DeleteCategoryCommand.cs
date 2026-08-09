using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Categories.DeleteCategory;

public record DeleteCategoryCommand(Guid Id) : IRequest<ErrorOr<Success>>;
