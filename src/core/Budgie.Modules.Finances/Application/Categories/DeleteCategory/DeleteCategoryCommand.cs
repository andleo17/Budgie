using ErrorOr;
using MediatR;

namespace Budgie.Modules.Finances.Application.Categories.DeleteCategory;

public record DeleteCategoryCommand(Guid Id) : IRequest<ErrorOr<Success>>;
