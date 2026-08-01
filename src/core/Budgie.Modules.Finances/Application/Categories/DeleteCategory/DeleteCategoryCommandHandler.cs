using Budgie.Modules.Finances.Infrastructure.Database;
using Budgie.Modules.Finances.Infrastructure.Logging;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Finances.Application.Categories.DeleteCategory;

internal sealed class DeleteCategoryCommandHandler(
    FinancesDbContext dbContext,
    ILogger<DeleteCategoryCommandHandler> logger)
    : IRequestHandler<DeleteCategoryCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (category is null) return Error.NotFound("Category.NotFound", "Category not found.");
        if (category.IsSystem) return Error.Forbidden("Category.IsSystem", "System categories cannot be deleted.");

        dbContext.Categories.Remove(category);
        await dbContext.SaveChangesAsync(cancellationToken);

        LogMessages.CategoryDeleted(logger, request.Id);

        return Result.Success;
    }
}
