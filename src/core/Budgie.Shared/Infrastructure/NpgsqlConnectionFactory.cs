using Npgsql;

namespace Budgie.Shared.Infrastructure;

public sealed class NpgsqlConnectionFactory(NpgsqlDataSource dataSource) : IDbConnectionFactory
{
    public NpgsqlConnection CreateConnection() => dataSource.CreateConnection();
}
