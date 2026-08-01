using ErrorOr;
using MediatR;

namespace Budgie.Modules.Finances.Application.Categories.GetCategories;

public record GetCategoriesQuery(bool IncludeSystem = false) : IRequest<ErrorOr<List<CategoryResponse>>>;
