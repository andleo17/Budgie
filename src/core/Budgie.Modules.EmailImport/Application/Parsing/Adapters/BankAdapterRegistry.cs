namespace Budgie.Modules.EmailImport.Application.Parsing.Adapters;

/// <inheritdoc cref="IBankAdapterRegistry"/>
/// <remarks>
/// Stateless: built once at startup from the registered adapters and shared as a singleton.
/// Duplicate keys fail fast at construction — two adapters answering to the same key would
/// otherwise make rule resolution depend on registration order.
/// </remarks>
internal sealed class BankAdapterRegistry : IBankAdapterRegistry
{
    // Held as the concrete Dictionary type (never mutated after construction) so Keys can be
    // exposed as IReadOnlyCollection<string> without copying.
    private readonly Dictionary<string, IBankAdapter> _adapters;

    /// <exception cref="InvalidOperationException">Two adapters share the same <see cref="IBankAdapter.Key"/>.</exception>
    public BankAdapterRegistry(IEnumerable<IBankAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(adapters);

        var map = new Dictionary<string, IBankAdapter>(StringComparer.Ordinal);
        foreach (var adapter in adapters)
        {
            if (!map.TryAdd(adapter.Key, adapter))
                throw new InvalidOperationException(
                    $"Duplicate bank adapter key '{adapter.Key}': every adapter must answer to a unique key.");
        }

        _adapters = map;
    }

    public IBankAdapter Get(string key)
    {
        if (!string.IsNullOrWhiteSpace(key) && _adapters.TryGetValue(key, out var adapter))
            return adapter;

        throw new InvalidOperationException(
            $"No bank adapter is registered for key '{key}'. Registered keys: {string.Join(", ", _adapters.Keys)}.");
    }

    public IReadOnlyCollection<string> Keys => _adapters.Keys;
}
