using Budgie.Shared.Infrastructure;
using Npgsql;

namespace Budgie.IntegrationTests;

/// <summary>
/// A tiny IDbConnectionFactory for tests that wraps a connection string directly.
/// Used to apply the schema + seed the database against the Testcontainer.
/// </summary>
public sealed class NpgsqlTestConnectionFactory(string connectionString) : IDbConnectionFactory
{
    public NpgsqlConnection CreateConnection() => new(connectionString);
}
