using ErrorOr;
using Mediator;

namespace Budgie.Modules.Finances.Application.Categories.CreateCategory;

public record CreateCategoryCommand(string Name, string? Color, string? Icon, int Type, Guid? ParentId = null) : IRequest<ErrorOr<Guid>>;
