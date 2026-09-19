using System.Data;
using System.Net.Http.Json;
using System.Text.Json;
using DotnetApiSample.Application.Abstractions;
using DotnetApiSample.Application.Messaging;
using DotnetApiSample.Application.Products;
using DotnetApiSample.Domain;
using DotnetApiSample.Infrastructure.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DotnetApiSample.IntegrationTests;

public class MessagingTests : IClassFixture<ProductsApiFactory>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ProductsApiFactory _factory;

    public MessagingTests(ProductsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateProduct_PublishesProductCreatedEvent()
    {
        await _factory.ResetAsync();
        await _factory.ResetMessagingAsync();

        var client = _factory.CreateClient();
        var request = new CreateProductRequest { Name = "Pour-over Set", Sku = "POV-004", Price = 32.00m };

        var response = await client.PostAsJsonAsync("/api/products", request);
        response.EnsureSuccessStatusCode();

        await using var connection = await RabbitMqConnectionFactory.ConnectAsync(_factory.RabbitMqSettings, CancellationToken.None);
        await using var channel = await connection.CreateChannelAsync();

        var delivery = await WaitForMessageAsync(channel, RabbitMqTopology.ProductCreatedQueue);
        var @event = JsonSerializer.Deserialize<ProductCreatedEvent>(delivery.Body.Span, SerializerOptions);

        Assert.NotNull(@event);
        Assert.Equal("POV-004", @event.Sku);
        Assert.Equal(32.00m, @event.Price);
        Assert.NotEqual(Guid.Empty, @event.EventId);
    }

    [Fact]
    public async Task Consumer_HandlesPublishedEvent_WritesAuditRow()
    {
        await _factory.ResetAsync();
        await _factory.ResetMessagingAsync();

        var client = _factory.CreateClient();
        var request = new CreateProductRequest { Name = "Pour-over Set", Sku = "POV-004", Price = 32.00m };

        var response = await client.PostAsJsonAsync("/api/products", request);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<Product>();

        Assert.NotNull(created);

        // The message waits in the queue, so starting the consumer afterwards removes any race.
        var consumer = CreateConsumer(new AuditProductCreatedHandler(new TestDbConnectionFactory(_factory.PostgresConnectionString)));
        await consumer.StartAsync(CancellationToken.None);

        try
        {
            var handled = await WaitForAsync(
                () => _factory.CountAuditRowsAsync(created.Id),
                count => count == 1);

            Assert.True(handled, "The consumer never wrote an audit row.");
        }
        finally
        {
            await StopAsync(consumer);
        }
    }

    [Fact]
    public async Task Consumer_PoisonMessage_RetriesThenDeadLetters()
    {
        await _factory.ResetAsync();
        await _factory.ResetMessagingAsync();

        var consumer = CreateConsumer(new ThrowingHandler());
        await consumer.StartAsync(CancellationToken.None);

        try
        {
            await using var connection = await RabbitMqConnectionFactory.ConnectAsync(_factory.RabbitMqSettings, CancellationToken.None);
            await using var publishChannel = await connection.CreateChannelAsync();
            await PublishAsync(publishChannel, new ProductCreatedEvent(Guid.NewGuid(), 42, "POISON-1", "Poison", 1.00m, DateTimeOffset.UtcNow));

            await using var channel = await connection.CreateChannelAsync();
            var delivery = await WaitForMessageAsync(channel, RabbitMqTopology.DeadLetterQueue, TimeSpan.FromSeconds(30));

            var attempts = _factory.RabbitMqSettings.RetryDelaysSeconds.Length;
            Assert.Equal(attempts, Assert.IsType<int>(delivery.BasicProperties.Headers![RabbitMqTopology.AttemptHeader]));

            var @event = JsonSerializer.Deserialize<ProductCreatedEvent>(delivery.Body.Span, SerializerOptions);
            Assert.NotNull(@event);
            Assert.Equal("POISON-1", @event.Sku);
        }
        finally
        {
            await StopAsync(consumer);
        }
    }

    private ProductCreatedConsumer CreateConsumer(IProductCreatedHandler handler)
        => new(_factory.RabbitMqSettings, handler, NullLogger<ProductCreatedConsumer>.Instance);

    private static async Task<PendingDelivery> WaitForMessageAsync(IChannel channel, string queue, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));

        while (DateTime.UtcNow < deadline)
        {
            var result = await channel.BasicGetAsync(queue, autoAck: true);
            if (result is not null)
            {
                return new PendingDelivery(result.Body, result.BasicProperties);
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"No message appeared on '{queue}' within the timeout.");
    }

    private static async Task<bool> WaitForAsync(Func<Task<long>> probe, Func<long, bool> isSatisfied)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);

        while (DateTime.UtcNow < deadline)
        {
            if (isSatisfied(await probe()))
            {
                return true;
            }

            await Task.Delay(100);
        }

        return false;
    }

    private static async Task PublishAsync(IChannel channel, ProductCreatedEvent @event)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(@event, SerializerOptions);
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent
        };

        await channel.BasicPublishAsync(
            RabbitMqTopology.Exchange,
            RabbitMqTopology.ProductCreatedRoutingKey,
            mandatory: true,
            basicProperties: properties,
            body: body);
    }

    private static async Task StopAsync(ProductCreatedConsumer consumer)
    {
        await consumer.StopAsync(CancellationToken.None);
        consumer.Dispose();
    }

    private sealed record PendingDelivery(ReadOnlyMemory<byte> Body, IReadOnlyBasicProperties BasicProperties);

    private sealed class ThrowingHandler : IProductCreatedHandler
    {
        public Task HandleAsync(ProductCreatedEvent @event, CancellationToken cancellationToken)
            => throw new InvalidOperationException("This handler always fails.");
    }

    private sealed class TestDbConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;

        public TestDbConnectionFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        public IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);
    }
}
