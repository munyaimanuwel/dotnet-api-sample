using System.Data;
using DotnetApiSample.Application.Abstractions;
using Npgsql;

namespace DotnetApiSample.Infrastructure.Persistence;

internal sealed class NpgsqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public NpgsqlConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);
}
