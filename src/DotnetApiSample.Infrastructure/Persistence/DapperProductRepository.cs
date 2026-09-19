using Dapper;
using DotnetApiSample.Application.Abstractions;
using DotnetApiSample.Domain;

namespace DotnetApiSample.Infrastructure.Persistence;

internal sealed class DapperProductRepository : IProductRepository
{
    private const string SelectColumns =
        "id AS Id, name AS Name, sku AS Sku, price AS Price, created_at AS CreatedAt";

    private readonly IDbConnectionFactory _connectionFactory;

    public DapperProductRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken)
    {
        var sql = $"SELECT {SelectColumns} FROM products ORDER BY id;";

        using var connection = _connectionFactory.CreateConnection();
        var products = await connection.QueryAsync<Product>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return products.AsList();
    }

    public async Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var sql = $"SELECT {SelectColumns} FROM products WHERE id = @Id;";

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<Product>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<Product> AddAsync(Product product, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO products (name, sku, price)
            VALUES (@Name, @Sku, @Price)
            RETURNING id AS Id, name AS Name, sku AS Sku, price AS Price, created_at AS CreatedAt;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleAsync<Product>(
            new CommandDefinition(sql, product, cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateAsync(Product product, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE products
            SET name = @Name, sku = @Sku, price = @Price
            WHERE id = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, product, cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM products WHERE id = @Id;";

        using var connection = _connectionFactory.CreateConnection();
        var affected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));

        return affected > 0;
    }
}
