using Budgie.Shared.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Budgie.Api.Infrastructure.Middlewares;

public class ConfigurationCheckMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IUserSettingsProvider settingsProvider)
    {
        var path = context.Request.Path.Value?.ToLower();

        // Allow access to health checks, auth and settings configuration
        if (path != null && (
            path.StartsWith("/api/users/login") || 
            path.StartsWith("/api/users/register") || 
            path.StartsWith("/api/users/refresh") ||
            path.StartsWith("/api/users/settings") ||
            path.StartsWith("/health")))
        {
            await next(context);
            return;
        }

        // Check if user is authenticated first
        if (context.User.Identity?.IsAuthenticated == true)
        {
            if (!await settingsProvider.IsConfiguredAsync())
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new 
                { 
                    Error = "ConfigurationRequired", 
                    Message = "You must configure your account before using the application." 
                });
                return;
            }
        }

        await next(context);
    }
}
