using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Categories.GetCategoryById;

public record GetCategoryByIdQuery(Guid Id) : IRequest<ErrorOr<CategoryResponse>>;
