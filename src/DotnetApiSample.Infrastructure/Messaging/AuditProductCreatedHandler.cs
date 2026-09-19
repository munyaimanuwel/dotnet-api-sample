using Dapper;
using DotnetApiSample.Application.Abstractions;
using DotnetApiSample.Application.Messaging;

namespace DotnetApiSample.Infrastructure.Messaging;

public sealed class AuditProductCreatedHandler : IProductCreatedHandler
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AuditProductCreatedHandler(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task HandleAsync(ProductCreatedEvent @event, CancellationToken cancellationToken)
    {
        const string sql = "INSERT INTO product_audit (product_id, sku) VALUES (@ProductId, @Sku);";

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { @event.ProductId, @event.Sku }, cancellationToken: cancellationToken));
    }
}
