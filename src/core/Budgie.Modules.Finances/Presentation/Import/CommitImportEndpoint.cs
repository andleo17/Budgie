using Budgie.Modules.Finances.Application.Import.CommitMoneyManagerImport;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Finances.Presentation.Import;

public static class CommitImportEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/money-manager/commit", async (IFormFile file, bool overwrite, IMediator mediator, CancellationToken ct) =>
        {
            if (file is null || file.Length <= 0)
                return Results.BadRequest(new Dictionary<string, string[]>
                {
                    ["Import.FileTooLarge"] = new[] { "A file must be provided." }
                });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            ms.Position = 0;

            var cmd = new CommitMoneyManagerImportCommand(ms, file.FileName, file.ContentType, overwrite);
            var result = await mediator.Send(cmd, ct);
            return result.Match(
                value => Results.Ok(value),
                errors => errors.Any(e => e.Code == "Import.AlreadyImported")
                    ? Results.Conflict(errors.ToDictionary(e => e.Code, e => new[] { e.Description }))
                    : Results.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
        })
        .RequireAuthorization()
        .DisableAntiforgery()
        .Accepts<IFormFile>("multipart/form-data")
        .Produces<CommitMoneyManagerImportResponse>(StatusCodes.Status200OK);
    }
}
