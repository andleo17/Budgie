using ErrorOr;
using MediatR;

namespace Budgie.Modules.Finances.Application.Categories.GetCategoryById;

public record GetCategoryByIdQuery(Guid Id) : IRequest<ErrorOr<CategoryResponse>>;
