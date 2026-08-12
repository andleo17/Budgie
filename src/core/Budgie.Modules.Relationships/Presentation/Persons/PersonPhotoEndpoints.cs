using Budgie.Shared.Abstractions;
using Budgie.Shared.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Relationships.Presentation.Persons;

internal static class PersonPhotoEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("persons/{id:guid}/photo", async (
            Guid id, HttpRequest request, IDbConnectionFactory factory, ICurrentUserProvider currentUserProvider,
            IWebHostEnvironment env) =>
        {
            var userId = currentUserProvider.GetUserId();
            var photoPath = await factory.QuerySingleOrDefaultAsync<string?>(
                @"SELECT ""PhotoPath"" FROM ""Persons"" WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
                new { Id = id, UserId = userId }, request.HttpContext.RequestAborted);
            if (photoPath is null) return Results.NotFound();

            var file = request.Form.Files.GetFile("file");
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "No file provided." });

            var uploadsDir = Path.Combine(WebRoot(env), "uploads", "persons");
            Directory.CreateDirectory(uploadsDir);

            var ext = Path.GetExtension(file.FileName);
            var fileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            if (photoPath.Length > 0)
            {
                var oldPath = Path.Combine(WebRoot(env), photoPath);
                if (File.Exists(oldPath)) File.Delete(oldPath);
            }

            await factory.ExecuteAsync(
                @"UPDATE ""Persons"" SET ""PhotoPath"" = @Path WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
                new { Path = $"uploads/persons/{fileName}", Id = id, UserId = userId },
                request.HttpContext.RequestAborted);

            return Results.Ok(new { photoUrl = $"/uploads/persons/{fileName}" });
        })
        .Produces(200)
        .Produces(400)
        .Produces(404)
        .WithName("UploadPersonPhoto")
        .WithTags("Persons")
        .DisableAntiforgery();

        group.MapDelete("persons/{id:guid}/photo", async (
            Guid id, HttpRequest request, IDbConnectionFactory factory, ICurrentUserProvider currentUserProvider,
            IWebHostEnvironment env) =>
        {
            var userId = currentUserProvider.GetUserId();
            var photoPath = await factory.QuerySingleOrDefaultAsync<string?>(
                @"SELECT ""PhotoPath"" FROM ""Persons"" WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
                new { Id = id, UserId = userId }, request.HttpContext.RequestAborted);
            if (photoPath is null) return Results.NotFound();

            if (photoPath.Length > 0)
            {
                var fullPath = Path.Combine(WebRoot(env), photoPath);
                if (File.Exists(fullPath)) File.Delete(fullPath);
            }

            await factory.ExecuteAsync(
                @"UPDATE ""Persons"" SET ""PhotoPath"" = NULL WHERE ""Id"" = @Id AND ""UserId"" = @UserId",
                new { Id = id, UserId = userId }, request.HttpContext.RequestAborted);

            return Results.NoContent();
        })
        .Produces(204)
        .Produces(404)
        .WithName("DeletePersonPhoto")
        .WithTags("Persons");
    }

    private static string WebRoot(IWebHostEnvironment env) =>
        env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
}
