using Budgie.Modules.EmailImport.Application.Abstractions;
using Budgie.Shared.Enums;

namespace Budgie.Modules.EmailImport.Infrastructure.Email.Gmail;

/// <summary>
/// Resolves the registered <see cref="IEmailProviderClient"/> for a given
/// <see cref="EmailProvider"/>, keyed by each client's <see cref="IEmailProviderClient.Provider"/>.
/// A duplicate provider registration fails fast in the constructor (same rationale as
/// <c>BankAdapterRegistry</c>); a missing one fails at <see cref="Resolve"/>, which the
/// sync worker catches, logs and skips for that connection.
/// </summary>
/// <remarks>
/// Registration note: a singleton lifetime is appropriate only if the registered
/// clients are also resolvable from the root provider (they are stateless beyond
/// singleton-safe dependencies). If clients are registered scoped, this resolver must
/// share that scope instead — a singleton capturing scoped clients is rejected by
/// scope validation in Development.
/// </remarks>
internal sealed class EmailProviderResolver(IEnumerable<IEmailProviderClient> clients) : IEmailProviderResolver
{
    private readonly IReadOnlyDictionary<EmailProvider, IEmailProviderClient> _clients =
        clients.ToDictionary(client => client.Provider);

    /// <inheritdoc />
    public IEmailProviderClient Resolve(EmailProvider provider) =>
        _clients.TryGetValue(provider, out var client)
            ? client
            : throw new InvalidOperationException(
                $"No email provider client is registered for provider '{provider}'.");
}
