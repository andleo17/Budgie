namespace Budgie.Modules.EmailImport.Application.Parsing;

/// <summary>
/// Lookup of the registered <see cref="IBankAdapter"/> implementations by their
/// <see cref="IBankAdapter.Key"/>. The <c>EmailProvider</c>-style resolver counterpart for
/// parsers: registered as a singleton because adapters are stateless.
/// </summary>
public interface IBankAdapterRegistry
{
    /// <summary>
    /// Returns the adapter registered under <paramref name="key"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">No adapter is registered for the key.</exception>
    IBankAdapter Get(string key);

    /// <summary>Keys of every registered adapter.</summary>
    IReadOnlyCollection<string> Keys { get; }
}
