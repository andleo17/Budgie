using Budgie.Shared.Enums;

namespace Budgie.Modules.EmailImport.Application.Abstractions;

/// <summary>
/// Resolves the <see cref="IEmailProviderClient"/> registered for a given
/// <see cref="EmailProvider"/>. <see cref="EmailConnection.Provider"/> discriminates the
/// registration; <see cref="EmailConnection.SyncCursor"/> is an opaque string whose
/// meaning is known only by the resolved provider.
/// </summary>
public interface IEmailProviderResolver
{
    /// <summary>
    /// Returns the client registered for <paramref name="provider"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// No implementation is registered for that provider. Callers (the sync worker)
    /// catch, log and skip the connection.
    /// </exception>
    IEmailProviderClient Resolve(EmailProvider provider);
}
