using System.Data;
using Dapper;
using Npgsql;

namespace Budgie.Shared.Infrastructure;

public static class DbConnectionExtensions
{
    public static async Task<IReadOnlyList<T>> QueryListAsync<T>(
        this IDbConnectionFactory factory,
        string sql,
        object? parameters = null,
        CancellationToken ct = default)
    {
        using var connection = factory.CreateConnection();
        var result = await connection.QueryAsync<T>(new CommandDefinition(sql, parameters, cancellationToken: ct));
        return result.AsList();
    }

    public static async Task<T?> QuerySingleOrDefaultAsync<T>(
        this IDbConnectionFactory factory,
        string sql,
        object? parameters = null,
        CancellationToken ct = default)
    {
        using var connection = factory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<T>(new CommandDefinition(sql, parameters, cancellationToken: ct));
    }

    public static async Task<T> QuerySingleAsync<T>(
        this IDbConnectionFactory factory,
        string sql,
        object? parameters = null,
        CancellationToken ct = default)
    {
        using var connection = factory.CreateConnection();
        return await connection.QuerySingleAsync<T>(new CommandDefinition(sql, parameters, cancellationToken: ct));
    }

    public static async Task<int> ExecuteAsync(
        this IDbConnectionFactory factory,
        string sql,
        object? parameters = null,
        CancellationToken ct = default)
    {
        using var connection = factory.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
    }

    public static async Task<T> ExecuteScalarAsync<T>(
        this IDbConnectionFactory factory,
        string sql,
        object? parameters = null,
        CancellationToken ct = default)
        where T : struct
    {
        using var connection = factory.CreateConnection();
        return await connection.ExecuteScalarAsync<T>(new CommandDefinition(sql, parameters, cancellationToken: ct));
    }

    /// <summary>
    /// Executes multiple queries in a single connection, returning a reader for each.
    /// Use with QueryMultiple to read multiple result sets.
    /// </summary>
    public static async Task<SqlMapper.GridReader> QueryMultipleAsync(
        this IDbConnectionFactory factory,
        string sql,
        object? parameters = null,
        CancellationToken ct = default)
    {
        var connection = factory.CreateConnection();
        return await connection.QueryMultipleAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
    }
}
