using System.Globalization;

namespace Budgie.Api.Infrastructure.Middlewares;

public class RequestCultureMiddleware(RequestDelegate next)
{
    private static readonly string[] s_supportedCultures = ["es", "en"];

    public async Task InvokeAsync(HttpContext context)
    {
        var acceptLanguage = context.Request.Headers.AcceptLanguage.ToString();
        var cultureName = ParseCulture(acceptLanguage);

        var culture = new CultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        await next(context);
    }

    private static string ParseCulture(string acceptLanguage)
    {
        if (string.IsNullOrWhiteSpace(acceptLanguage))
            return "es";

        // Accept-Language: es-MX,es;q=0.9,en;q=0.8
        foreach (var part in acceptLanguage.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var range = part.Split(';')[0].Trim();
            if (string.IsNullOrWhiteSpace(range))
                continue;

            var lang = range.Split('-')[0].ToLowerInvariant();
            if (s_supportedCultures.Contains(lang))
                return lang;
        }

        return "es";
    }
}
