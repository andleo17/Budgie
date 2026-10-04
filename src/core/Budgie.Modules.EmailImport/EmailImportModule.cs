using Budgie.Modules.EmailImport.Application;
using Budgie.Modules.EmailImport.Application.Abstractions;
using Budgie.Modules.EmailImport.Application.Connections;
using Budgie.Modules.EmailImport.Application.Inbox;
using Budgie.Modules.EmailImport.Application.Inbox.Abstractions;
using Budgie.Modules.EmailImport.Application.Parsing;
using Budgie.Modules.EmailImport.Application.Parsing.Adapters;
using Budgie.Modules.EmailImport.Infrastructure.Database;
using Budgie.Modules.EmailImport.Infrastructure.Email.Gmail;
using Budgie.Modules.EmailImport.Infrastructure.Identity;
using Budgie.Modules.EmailImport.Infrastructure.Parsing;
using Budgie.Modules.EmailImport.Infrastructure.Security;
using Budgie.Modules.EmailImport.Infrastructure.Worker;
using Budgie.Modules.EmailImport.Presentation.Connections;
using Budgie.Modules.EmailImport.Presentation.Inbox;
using Budgie.Modules.EmailImport.Presentation.Rules;
using Budgie.Shared.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Budgie.Modules.EmailImport;

/// <summary>
/// Bootstrap of the EmailImport module: service registration and endpoint mapping.
/// </summary>
public static class EmailImportModule
{
    /// <summary>
    /// Registers the module's services. <paramref name="configuration"/> is required to
    /// read the <c>EmailImport:Enabled</c> master switch, which decides whether the
    /// background sync worker is registered at all.
    /// </summary>
    public static IServiceCollection AddEmailImportModule(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EmailImportOptions>(
            configuration.GetSection(EmailImportOptions.SectionName));
        services.Configure<GmailOptions>(
            configuration.GetSection(GmailOptions.SectionName));

        // ── Repositories (scoped; multi-tenancy lives in every signature) ──────────────
        services.AddScoped<IEmailConnectionRepository, EmailConnectionRepository>();
        services.AddScoped<IEmailImportRuleRepository, EmailImportRuleRepository>();
        services.AddScoped<IPendingEmailTransactionRepository, PendingEmailTransactionRepository>();

        // ── Singletons, each justified ──────────────────────────────────────────────────
        // ISecretProtector: stateless (read-only key, one AesGcm per call). Singleton also
        //   keeps the key derivation (and its one-time fallback warning) out of the scopes.
        // IBankAdapter + IBankAdapterRegistry: stateless apart from the compiled-regex
        //   cache, which is exactly the state a singleton should own.
        // OAuthStateService: the consumed-nonce dictionary is process-wide state — a nonce
        //   must burn across every scope, or a replay inside the 10-minute window would pass.
        services.AddSingleton<ISecretProtector, AesSecretProtector>();
        services.AddSingleton<IBankAdapter, GenericRegexAdapter>();
        services.AddSingleton<IBankAdapterRegistry, BankAdapterRegistry>();
        services.AddSingleton<OAuthStateService>();

        // ── Scoped, one instance per request (or per worker tick) ──────────────────────
        services.AddScoped<IMessageTextExtractor, MessageTextExtractor>();
        services.AddScoped<IEmailParser, EmailParser>();
        services.AddScoped<IEmailSyncService, EmailSyncService>();

        // Typed HttpClient for the provider REST API. BaseAddress and Timeout come from
        // GmailOptions so nothing provider-specific is hardcoded at the call sites.
        // The client is registered under ITS PORT too: GmailEmailProvider depends on
        // IGmailMailboxApi (so the cursor logic can be unit-tested without HTTP), and a
        // typed AddHttpClient only registers the concrete type — the interface would
        // otherwise be unresolvable and the app would fail at startup under
        // ValidateScopes.
        services.AddHttpClient<GmailApiClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<GmailOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseAddress);
            client.Timeout = options.Timeout;
        });
        services.AddScoped<IGmailMailboxApi>(sp =>
            ActivatorUtilities.GetServiceOrCreateInstance<GmailApiClient>(sp));
        services.AddScoped<IEmailProviderClient, GmailEmailProvider>();

        // IEmailProviderResolver MUST be scoped, never singleton: it receives
        // IEnumerable<IEmailProviderClient> by constructor and the clients are scoped
        // (GmailEmailProvider depends on the typed HttpClient). As a singleton it would
        // resolve its clients from the root provider and the app would fail at startup
        // under ValidateScopes (active in Development) with
        // "Cannot consume scoped service from singleton". The worker opens its own scope
        // per tick, so it obtains a correctly scoped resolver too.
        services.AddScoped<IEmailProviderResolver, EmailProviderResolver>();

        // ── Current user for the WHOLE service tree ────────────────────────────────────
        // ScopedCurrentUserProvider implements BOTH ICurrentUserProvider and
        // ICurrentUserProviderAccessor on one scoped instance, and it REPLACES the
        // HttpContext-only registration that used to live in Budgie.Api's Program.cs:
        //   - an explicitly pushed user (EmailSyncWorker per connection, manual review
        //     dispatch) wins — that is what lets CreateTransactionCommand run outside an
        //     HTTP request through the exact same path;
        //   - with nobody pushed (a normal HTTP request) it falls back to the JWT claim,
        //     resolving the owner exactly like the previous implementation did.
        // Both interfaces must map to the SAME instance or the pushed user would be
        // invisible to handlers resolving ICurrentUserProvider.
        services.AddScoped<ScopedCurrentUserProvider>();
        services.AddScoped<ICurrentUserProvider>(
            sp => sp.GetRequiredService<ScopedCurrentUserProvider>());
        services.AddScoped<ICurrentUserProviderAccessor>(
            sp => sp.GetRequiredService<ScopedCurrentUserProvider>());

        // ── Background worker, only when the module is enabled ─────────────────────────
        // EmailImport:Enabled defaults to false in appsettings.json: with the worker off,
        // integration tests never race a timer and SyncNow owns the schedule. The operator
        // turns the worker on per deployment (EMAILIMPORT_ENABLED), never per request.
        if (configuration.GetValue<bool>(EmailImportOptions.SectionName + ":Enabled"))
            services.AddHostedService<EmailSyncWorker>();

        return services;
    }

    public static IEndpointRouteBuilder MapEmailImportEndpoints(this IEndpointRouteBuilder app)
    {
        // Every route of the module requires a JWT, except the OAuth callback below.
        var group = app.MapGroup("/api/email-import").RequireAuthorization();

        // Deliberately mapped OUTSIDE the protected group: the provider's redirect arrives
        // without Budgie's JWT (it lives in localStorage), so the callback authenticates
        // through the signed single-use state alone. See CompleteConnectionEndpoint for
        // the full rationale — it is the one endpoint of the module without authorization
        // and it must stay that way (spec §6).
        CompleteConnectionEndpoint.MapEndpoint(app);

        GetAuthorizationUriEndpoint.MapEndpoint(group);
        ConnectionsEndpoints.MapEndpoints(group);
        RulesEndpoints.MapEndpoints(group);
        PreviewRuleEndpoint.MapEndpoint(group);
        InboxEndpoints.MapEndpoints(group);
        SyncNowEndpoint.MapEndpoint(group);
        return app;
    }
}
