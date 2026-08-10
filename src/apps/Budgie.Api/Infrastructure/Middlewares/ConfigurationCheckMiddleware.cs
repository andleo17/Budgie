using System.Text.Json;
using Budgie.Shared.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Budgie.Api.Infrastructure.Middlewares;

public class ConfigurationCheckMiddleware(RequestDelegate next)
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task InvokeAsync(HttpContext context, IUserSettingsProvider settingsProvider)
    {
        var path = context.Request.Path.Value?.ToLower();

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

        if (context.User.Identity?.IsAuthenticated == true)
        {
            if (!await settingsProvider.IsConfiguredAsync())
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                var json = JsonSerializer.Serialize(new 
                { 
                    Error = "ConfigurationRequired", 
                    Message = "You must configure your account before using the application." 
                }, s_jsonOptions);
                await context.Response.WriteAsync(json);
                return;
            }
        }

        await next(context);
    }
}
