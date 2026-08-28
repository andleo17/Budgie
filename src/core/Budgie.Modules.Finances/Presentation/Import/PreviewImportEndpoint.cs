using Budgie.Modules.Finances.Application.Import.PreviewMoneyManagerImport;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Finances.Presentation.Import;

public static class PreviewImportEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/money-manager/preview", async (IFormFile file, IMediator mediator, CancellationToken ct) =>
        {
            if (file is null || file.Length <= 0)
                return Results.BadRequest(new Dictionary<string, string[]>
                {
                    ["Import.FileTooLarge"] = new[] { "A file must be provided." }
                });

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            ms.Position = 0;

            var cmd = new PreviewMoneyManagerImportCommand(ms, file.FileName, file.ContentType);
            var result = await mediator.Send(cmd, ct);
            return result.Match(
                value => Results.Ok(value),
                errors => Results.BadRequest(errors.ToDictionary(e => e.Code, e => new[] { e.Description })));
        })
        .RequireAuthorization()
        .DisableAntiforgery()
        .Accepts<IFormFile>("multipart/form-data")
        .Produces<PreviewMoneyManagerImportResponse>(StatusCodes.Status200OK);
    }
}
