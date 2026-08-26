using Budgie.Modules.Relationships.Application;
using Budgie.Modules.Relationships.Application.Persons;
using Budgie.Modules.Relationships.Application.Tags;
using Budgie.Shared.Infrastructure;

namespace Budgie.Modules.Relationships.Infrastructure.Database;

internal sealed class PersonRepository(IDbConnectionFactory factory) : IPersonRepository
{
    public async Task<PersonResponse?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var row = await factory.QuerySingleOrDefaultAsync<PersonRow>(
            @"SELECT ""Id"",""Name"",""PhotoPath"" FROM ""Persons"" WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new { Id = id, UserId = userId }, ct);

        if (row is null) return null;

        var tags = (await factory.QueryListAsync<TagRow>(
            @"SELECT t.""Id"", t.""Name"", t.""Color"" FROM ""Tags"" t JOIN ""PersonTags"" pt ON pt.""TagId"" = t.""Id"" WHERE pt.""PersonId"" = @PId ORDER BY t.""Name""",
            new { PId = id }, ct)).ToList();

        return new PersonResponse(row.Id, row.Name, row.PhotoPath,
            tags.Select(t => new TagDto(t.Id, t.Name, t.Color)).ToList());
    }

    public async Task<List<PersonResponse>> GetByUserIdAsync(Guid userId, CancellationToken ct)
    {
        var persons = (await factory.QueryListAsync<PersonRow>(
            @"SELECT ""Id"",""Name"",""PhotoPath"" FROM ""Persons"" WHERE ""UserId"" = @UserId ORDER BY ""Name""",
            new { UserId = userId }, ct)).ToList();

        var personTags = (await factory.QueryListAsync<PersonTagRow>(
            @"SELECT pt.""PersonId"", t.""Id"" AS TagId, t.""Name"" AS TagName, t.""Color"" AS TagColor
              FROM ""PersonTags"" pt JOIN ""Tags"" t ON t.""Id"" = pt.""TagId""
              WHERE t.""UserId"" = @UserId
              ORDER BY t.""Name""",
            new { UserId = userId }, ct)).ToList();

        var tagMap = personTags.GroupBy(pt => pt.PersonId)
            .ToDictionary(g => g.Key, g => g.Select(x => new TagDto(x.TagId, x.TagName, x.TagColor)).ToList());

        return persons.Select(p => new PersonResponse(
            p.Id, p.Name, p.PhotoPath,
            tagMap.TryGetValue(p.Id, out var tags) ? tags : []
        )).ToList();
    }

    public async Task AddAsync(Guid id, Guid userId, string name, List<string>? tagNames, CancellationToken ct)
    {
        await factory.ExecuteAsync(
            @"INSERT INTO ""Persons"" (""Id"",""UserId"",""Name"",""CreatedAt"") VALUES (@Id,@UserId,@Name,NOW())",
            new { Id = id, UserId = userId, Name = name }, ct);

        if (tagNames is { Count: > 0 })
        {
            foreach (var tagName in tagNames)
            {
                var tagId = await GetOrCreateTagAsync(userId, tagName, ct);

                await factory.ExecuteAsync(
                    @"INSERT INTO ""PersonTags"" (""PersonId"",""TagId"") VALUES (@PId,@TId) ON CONFLICT DO NOTHING",
                    new { PId = id, TId = tagId }, ct);
            }
        }
    }

    public async Task<bool> UpdateAsync(Guid id, Guid userId, string name, List<string>? tagNames, CancellationToken ct)
    {
        var affected = await factory.ExecuteAsync(
            @"UPDATE ""Persons"" SET ""Name"" = @Name WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new { Id = id, UserId = userId, Name = name }, ct);

        if (affected == 0) return false;

        await factory.ExecuteAsync(@"DELETE FROM ""PersonTags"" WHERE ""PersonId"" = @Id", new { Id = id }, ct);

        if (tagNames is { Count: > 0 })
        {
            foreach (var tagName in tagNames)
            {
                var tagId = await GetOrCreateTagAsync(userId, tagName, ct);

                await factory.ExecuteAsync(
                    @"INSERT INTO ""PersonTags"" (""PersonId"",""TagId"") VALUES (@PId,@TId) ON CONFLICT DO NOTHING",
                    new { PId = id, TId = tagId }, ct);
            }
        }

        return true;
    }

    private async Task<Guid> GetOrCreateTagAsync(Guid userId, string tagName, CancellationToken ct)
    {
        var tag = await factory.QuerySingleOrDefaultAsync<TagRow>(
            @"SELECT ""Id"",""Name"",""Color"" FROM ""Tags"" WHERE ""UserId"" = @UserId AND ""Name"" = @Name",
            new { UserId = userId, Name = tagName }, ct);

        if (tag is not null) return tag.Id;

        var newId = Guid.NewGuid();
        await factory.ExecuteAsync(
            @"INSERT INTO ""Tags"" (""Id"",""UserId"",""Name"",""Color"",""CreatedAt"") VALUES (@Id,@UserId,@Name,@Color,NOW())",
            new { Id = newId, UserId = userId, Name = tagName, Color = (string?)null }, ct);
        return newId;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var affected = await factory.ExecuteAsync(
            @"DELETE FROM ""Persons"" WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new { Id = id, UserId = userId }, ct);
        return affected > 0;
    }

    public async Task<bool> UpdatePhotoPathAsync(Guid id, Guid userId, string? photoPath, CancellationToken ct)
    {
        var affected = await factory.ExecuteAsync(
            @"UPDATE ""Persons"" SET ""PhotoPath"" = @PhotoPath WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new { Id = id, UserId = userId, PhotoPath = photoPath }, ct);
        return affected > 0;
    }

    private record PersonRow(Guid Id, string Name, string? PhotoPath);
    private record PersonTagRow(Guid PersonId, Guid TagId, string TagName, string? TagColor);
    private record TagRow(Guid Id, string Name, string? Color);
}

internal sealed class TagRepository(IDbConnectionFactory factory) : ITagRepository
{
    public async Task<List<TagResponse>> GetByUserIdAsync(Guid userId, CancellationToken ct)
    {
        var rows = await factory.QueryListAsync<TagResponse>(
            @"SELECT ""Id"",""Name"",""Color"" FROM ""Tags"" WHERE ""UserId"" = @UserId ORDER BY ""Name""",
            new { UserId = userId }, ct);
        return rows.ToList();
    }

    public async Task<Guid> AddAsync(Guid userId, string name, string? color, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        await factory.ExecuteAsync(
            @"INSERT INTO ""Tags"" (""Id"",""UserId"",""Name"",""Color"",""CreatedAt"") VALUES (@Id,@UserId,@Name,@Color,NOW())",
            new { Id = id, UserId = userId, Name = name, Color = color }, ct);
        return id;
    }

    public async Task<bool> UpdateAsync(Guid id, Guid userId, string name, string? color, CancellationToken ct)
    {
        var affected = await factory.ExecuteAsync(
            @"UPDATE ""Tags"" SET ""Name"" = @Name, ""Color"" = @Color WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new { Id = id, UserId = userId, Name = name, Color = color }, ct);
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var affected = await factory.ExecuteAsync(
            @"DELETE FROM ""Tags"" WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
            new { Id = id, UserId = userId }, ct);
        return affected > 0;
    }
}
