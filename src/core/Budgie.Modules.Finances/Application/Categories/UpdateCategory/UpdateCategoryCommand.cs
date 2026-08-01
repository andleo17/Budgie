using ErrorOr;
using MediatR;

namespace Budgie.Modules.Finances.Application.Categories.UpdateCategory;

public record UpdateCategoryCommand(Guid Id, string Name, string? Color, string? Icon, int? Type = null, Guid? ParentId = null) : IRequest<ErrorOr<Success>>;
