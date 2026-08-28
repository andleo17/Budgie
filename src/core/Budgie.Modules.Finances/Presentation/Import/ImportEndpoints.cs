using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Budgie.Modules.Finances.Presentation.Import;

internal static class ImportEndpoints
{
    public static IEndpointRouteBuilder MapImportEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/import").RequireAuthorization();
        PreviewImportEndpoint.MapEndpoint(group);
        CommitImportEndpoint.MapEndpoint(group);
        return app;
    }
}
