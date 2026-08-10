using Npgsql;

namespace Budgie.Shared.Infrastructure;

public interface IDbConnectionFactory
{
    NpgsqlConnection CreateConnection();
}
