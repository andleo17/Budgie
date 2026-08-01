using Budgie.Modules.Relationships.Infrastructure.Database;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Budgie.Modules.Relationships.Presentation.Persons;

internal static class PersonPhotoEndpoints
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("persons/{id:guid}/photo", async (Guid id, HttpRequest request, RelationshipsDbContext dbContext) =>
        {
            var person = await dbContext.Persons.FirstOrDefaultAsync(p => p.Id == id);
            if (person is null) return Results.NotFound();

            var file = request.Form.Files.GetFile("file");
            if (file is null || file.Length == 0)
                return Results.BadRequest(new { error = "No file provided." });

            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "persons");
            Directory.CreateDirectory(uploadsDir);

            var ext = Path.GetExtension(file.FileName);
            var fileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            // Delete old photo if exists
            if (person.PhotoPath is not null)
            {
                var oldPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", person.PhotoPath);
                if (File.Exists(oldPath)) File.Delete(oldPath);
            }

            person.SetPhotoPath($"uploads/persons/{fileName}");
            await dbContext.SaveChangesAsync();

            return Results.Ok(new { photoUrl = $"/uploads/persons/{fileName}" });
        })
        .Produces(200)
        .WithName("UploadPersonPhoto")
        .WithTags("Persons")
        .DisableAntiforgery();

        app.MapDelete("persons/{id:guid}/photo", async (Guid id, RelationshipsDbContext dbContext) =>
        {
            var person = await dbContext.Persons.FirstOrDefaultAsync(p => p.Id == id);
            if (person is null) return Results.NotFound();

            if (person.PhotoPath is not null)
            {
                var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", person.PhotoPath);
                if (File.Exists(fullPath)) File.Delete(fullPath);
                person.SetPhotoPath(null);
                await dbContext.SaveChangesAsync();
            }

            return Results.NoContent();
        })
        .Produces(204)
        .WithName("DeletePersonPhoto")
        .WithTags("Persons");
    }
}
