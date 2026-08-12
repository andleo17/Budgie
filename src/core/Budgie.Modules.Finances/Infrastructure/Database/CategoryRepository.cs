using Budgie.Modules.Finances.Application;
using Budgie.Modules.Finances.Domain.Categories;
using Budgie.Shared.Domain.ValueObjects;
using Budgie.Shared.Infrastructure;
using Dapper;

namespace Budgie.Modules.Finances.Infrastructure.Database;

internal sealed class CategoryRepository(IDbConnectionFactory factory) : ICategoryRepository
{
    public async Task<Category?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var row = await factory.QuerySingleOrDefaultAsync<CategoryRow>(
            @"SELECT ""Id"",""UserId"",""Name"",""Color"",""Icon"",""Type"",""IsSystem"",""ParentId""
              FROM ""Categories"" WHERE ""Id"" = @Id AND (""UserId"" = @UserId OR ""UserId"" IS NULL)",
            new { Id = id, UserId = userId }, ct);
        return row is null ? null : Map(row);
    }

    public async Task<List<Category>> GetByUserIdAsync(Guid userId, CancellationToken ct)
        => (await factory.QueryListAsync<CategoryRow>(
            @"SELECT ""Id"",""UserId"",""Name"",""Color"",""Icon"",""Type"",""IsSystem"",""ParentId""
              FROM ""Categories"" WHERE ""UserId"" = @UserId",
            new { UserId = userId }, ct)).Select(Map).ToList();

    public Task AddAsync(Category category, CancellationToken ct)
        => factory.ExecuteAsync(
            @"INSERT INTO ""Categories"" (""Id"",""UserId"",""Name"",""Color"",""Icon"",""Type"",""IsSystem"",""ParentId"")
              VALUES (@Id,@UserId,@Name,@Color,@Icon,@Type,@IsSystem,@ParentId)",
            new
            {
                category.Id, category.UserId, Name = category.Name.Value,
                Color = category.Color?.Value, category.Icon, category.Type,
                category.IsSystem, category.ParentId
            }, ct);

    public Task UpdateAsync(Category category, CancellationToken ct)
        => factory.ExecuteAsync(
            @"UPDATE ""Categories"" SET ""Name"" = @Name, ""Color"" = @Color, ""Icon"" = @Icon,
              ""Type"" = @Type, ""ParentId"" = @ParentId WHERE ""Id"" = @Id",
            new
            {
                Name = category.Name.Value, Color = category.Color?.Value,
                category.Icon, category.Type, category.ParentId, category.Id
            }, ct);

    public Task DeleteAsync(Guid id, Guid userId, CancellationToken ct)
        => factory.ExecuteAsync(@"DELETE FROM ""Categories"" WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new { Id = id, UserId = userId }, ct);

    private static Category Map(CategoryRow row) => new()
    {
        Id = row.Id,
        UserId = row.UserId,
        Name = Name.From(row.Name, 50),
        Color = HexColor.FromNullable(row.Color),
        Icon = row.Icon,
        Type = row.Type,
        IsSystem = row.IsSystem,
        ParentId = row.ParentId
    };

    private record CategoryRow(Guid Id, Guid? UserId, string Name, string? Color, string? Icon,
        int Type, bool IsSystem, Guid? ParentId);
}
