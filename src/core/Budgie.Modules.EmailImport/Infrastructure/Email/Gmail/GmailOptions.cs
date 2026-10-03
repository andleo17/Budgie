namespace Budgie.Modules.EmailImport.Infrastructure.Email.Gmail;

/// <summary>
/// Configuration for the Gmail provider implementation, bound from
/// <c>EmailImport:Providers:gmail</c>. Vendor-specific naming is allowed here — this is
/// Infrastructure, the one layer permitted to know which vendor it talks to.
/// </summary>
/// <remarks>
/// Intended registration (wired in the module bootstrap, slice 13):
/// <code>
/// services.Configure&lt;GmailOptions&gt;(config.GetSection(GmailOptions.SectionName));
/// services.AddHttpClient&lt;GmailApiClient&gt;((sp, client) =>
/// {
///     var options = sp.GetRequiredService&lt;IOptions&lt;GmailOptions&gt;&gt;().Value;
///     client.BaseAddress = new Uri(options.BaseAddress);
///     client.Timeout = options.Timeout;   // 30 s by default; NOT set inside GmailApiClient
/// });
/// </code>
/// </remarks>
public sealed class GmailOptions
{
    /// <summary>Configuration section this provider is bound from.</summary>
    public const string SectionName = "EmailImport:Providers:gmail";

    /// <summary>OAuth client id from the provider console. Required.</summary>
    public string? ClientId { get; set; }

    /// <summary>OAuth client secret from the provider console. Required.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Base address of the Gmail REST API (note the trailing slash and version segment).</summary>
    public string BaseAddress { get; set; } = "https://gmail.googleapis.com/gmail/v1/";

    /// <summary>Base address of the provider's OAuth authorization endpoint.</summary>
    public string AuthBaseAddress { get; set; } = "https://accounts.google.com/";

    /// <summary>Absolute URL of the OAuth token endpoint (authorization code exchange and refresh).</summary>
    public string TokenUrl { get; set; } = "https://oauth2.googleapis.com/token";

    /// <summary>
    /// OAuth scopes requested. Defaults to read-only mail access: Budgie reads the
    /// mailbox but can never modify it or mark messages as read.
    /// </summary>
    public List<string> Scopes { get; set; } =
    [
        "https://www.googleapis.com/auth/gmail.readonly"
    ];

    /// <summary>
    /// HTTP timeout applied to the <see cref="GmailApiClient"/> HttpClient at
    /// registration time (default 30 seconds).
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
