using Budgie.Shared.Abstractions;
using Budgie.Shared.Infrastructure;
using ErrorOr;
using Mediator;

namespace Budgie.Modules.Relationships.Application.Tags;

public record TagResponse(Guid Id, string Name, string? Color);

public record CreateTagCommand(string Name, string? Color) : IRequest<ErrorOr<Guid>>;

internal sealed class CreateTagCommandHandler(
    IDbConnectionFactory factory, ICurrentUserProvider currentUserProvider)
    : IRequestHandler<CreateTagCommand, ErrorOr<Guid>>
{
    public async ValueTask<ErrorOr<Guid>> Handle(CreateTagCommand request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var id = Guid.NewGuid();
        await factory.ExecuteAsync(
            @"INSERT INTO ""Tags"" (""Id"",""UserId"",""Name"",""Color"",""CreatedAt"") VALUES (@Id,@UserId,@Name,@Color,NOW())",
            new { Id = id, UserId = userId, request.Name, request.Color }, ct);
        return id;
    }
}

public record GetTagsQuery : IRequest<ErrorOr<List<TagResponse>>>;

internal sealed class GetTagsQueryHandler(
    IDbConnectionFactory factory, ICurrentUserProvider currentUserProvider)
    : IRequestHandler<GetTagsQuery, ErrorOr<List<TagResponse>>>
{
    public async ValueTask<ErrorOr<List<TagResponse>>> Handle(GetTagsQuery request, CancellationToken ct)
    {
        var userId = currentUserProvider.GetUserId();
        var tags = await factory.QueryListAsync<TagResponse>(
            @"SELECT ""Id"",""Name"",""Color"" FROM ""Tags"" WHERE ""UserId"" = @UserId",
            new { UserId = userId }, ct);
        return tags.ToList();
    }
}
