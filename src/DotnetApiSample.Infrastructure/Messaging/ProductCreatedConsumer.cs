using System.Text;
using System.Text.Json;
using DotnetApiSample.Application.Abstractions;
using DotnetApiSample.Application.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DotnetApiSample.Infrastructure.Messaging;

public sealed class ProductCreatedConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly RabbitMqOptions _options;
    private readonly IProductCreatedHandler _handler;
    private readonly ILogger<ProductCreatedConsumer> _logger;

    public ProductCreatedConsumer(
        RabbitMqOptions options,
        IProductCreatedHandler handler,
        ILogger<ProductCreatedConsumer> logger)
    {
        _options = options;
        _handler = handler;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var connection = await RabbitMqConnectionFactory.ConnectAsync(_options, stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await RabbitMqTopology.DeclareAsync(channel, _options, stoppingToken);

        // One unacknowledged message at a time, so a failing message cannot be handed out again
        // in parallel and retries stay deterministic.
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, args) => ProcessAsync(channel, args);

        await channel.BasicConsumeAsync(
            RabbitMqTopology.ProductCreatedQueue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        _logger.LogInformation(
            "Consuming {Queue} with {MaxAttempts} retry attempt(s) before the DLQ.",
            RabbitMqTopology.ProductCreatedQueue,
            _options.RetryDelaysSeconds.Length);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ProcessAsync(IChannel channel, BasicDeliverEventArgs args)
    {
        var attempt = ReadAttempt(args.BasicProperties);
        var maxAttempts = _options.RetryDelaysSeconds.Length;

        try
        {
            var @event = JsonSerializer.Deserialize<ProductCreatedEvent>(args.Body.Span, SerializerOptions)
                ?? throw new InvalidOperationException("Message body could not be deserialized.");

            await _handler.HandleAsync(@event, CancellationToken.None);

            // Manual ack: the message is only removed once handling has actually succeeded.
            await channel.BasicAckAsync(args.DeliveryTag, multiple: false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Handling {RoutingKey} failed on attempt {Attempt} of {MaxAttempts}.",
                args.RoutingKey,
                attempt + 1,
                maxAttempts);

            await RouteFailureAsync(channel, args, attempt, maxAttempts);
        }
    }

    private async Task RouteFailureAsync(IChannel channel, BasicDeliverEventArgs args, int attempt, int maxAttempts)
    {
        if (attempt < maxAttempts)
        {
            var nextAttempt = attempt + 1;

            // Republish onto the retry exchange; that queue's TTL provides the backoff before it
            // dead-letters back onto the main exchange.
            var properties = new BasicProperties
            {
                ContentType = args.BasicProperties.ContentType,
                DeliveryMode = DeliveryModes.Persistent,
                Headers = new Dictionary<string, object?> { [RabbitMqTopology.AttemptHeader] = nextAttempt }
            };

            await channel.BasicPublishAsync(
                RabbitMqTopology.RetryExchange,
                RabbitMqTopology.RetryRoutingKey(nextAttempt),
                mandatory: true,
                basicProperties: properties,
                body: args.Body,
                cancellationToken: CancellationToken.None);

            await channel.BasicAckAsync(args.DeliveryTag, multiple: false);
            return;
        }

        // Retries exhausted: nack without requeue so the main queue's DLX moves it to the DLQ.
        await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cancellationToken: CancellationToken.None);
    }

    private static int ReadAttempt(IReadOnlyBasicProperties? properties)
    {
        if (properties?.Headers is null ||
            !properties.Headers.TryGetValue(RabbitMqTopology.AttemptHeader, out var value) ||
            value is null)
        {
            return 0;
        }

        return value switch
        {
            int attempt => attempt,
            long attempt => (int)attempt,
            byte[] bytes => int.TryParse(Encoding.UTF8.GetString(bytes), out var parsed) ? parsed : 0,
            _ => 0
        };
    }
}
