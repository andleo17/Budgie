using Budgie.Modules.Finances.Application.Export;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Text;

namespace Budgie.Modules.Finances.Presentation.Export;

internal static class ExportEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("/export/transactions", async (
            string format = "csv", DateTime? startDate = null, DateTime? endDate = null,
            Guid? accountId = null, IMediator mediator = null!) =>
        {
            var fmt = string.Equals(format, "json", StringComparison.OrdinalIgnoreCase)
                ? ExportFormat.Json : ExportFormat.Csv;

            var result = await mediator.Send(new ExportTransactionsQuery(fmt, startDate, endDate, accountId));
            if (result.IsError) return Results.BadRequest(result.Errors);

            var bytes = Encoding.UTF8.GetBytes(result.Value);
            var contentType = fmt == ExportFormat.Csv ? "text/csv" : "application/json";
            var extension = fmt == ExportFormat.Csv ? "csv" : "json";
            return Results.File(bytes, contentType,
                $"budgie-transactions-{DateTime.UtcNow:yyyyMMddHHmmss}.{extension}");
        })
        .Produces(200)
        .Produces(400)
        .WithName("ExportTransactions")
        .WithTags("Export");

        group.MapGet("/export/snapshot", async (IMediator mediator) =>
        {
            var result = await mediator.Send(new ExportSnapshotQuery());
            return result.Match(
                value => Results.Ok(value),
                errors => Results.BadRequest(errors));
        })
        .Produces<SnapshotData>(200)
        .WithName("ExportSnapshot")
        .WithTags("Export");

        group.MapPost("/export/snapshot", async (RestoreSnapshotCommand command, IMediator mediator) =>
        {
            var result = await mediator.Send(command);
            return result.Match(
                _ => Results.Ok(new { restored = true }),
                errors => Results.BadRequest(errors));
        })
        .Produces(200)
        .Produces(400)
        .WithName("RestoreSnapshot")
        .WithTags("Export");
    }
}
