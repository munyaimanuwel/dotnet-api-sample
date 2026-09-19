using DotnetApiSample.Application.Messaging;
using RabbitMQ.Client;

namespace DotnetApiSample.Infrastructure.Messaging;

public static class RabbitMqTopology
{
    public const string Exchange = "products";
    public const string RetryExchange = "products.retry";
    public const string DeadLetterExchange = "products.dlx";

    public const string ProductCreatedRoutingKey = MessageRoutingKeys.ProductCreated;
    public const string ProductCreatedQueue = "products.created";
    public const string DeadLetterQueue = "products.created.dlq";

    /// <summary>Custom header carrying how many times the message has been retried.</summary>
    public const string AttemptHeader = "attempt";

    public static string RetryQueue(int attempt) => $"products.created.retry.{attempt}";

    public static string RetryRoutingKey(int attempt) => $"retry.{attempt}";

    /// <summary>The producer only needs the exchange; queues and bindings belong to the consumer.</summary>
    public static async Task DeclareExchangeAsync(IChannel channel, CancellationToken cancellationToken = default)
        => await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, cancellationToken: cancellationToken);

    /// <summary>
    /// Declares the full graph idempotently, so the consumer and the tests can each call it
    /// without caring about ordering.
    /// </summary>
    public static async Task DeclareAsync(
        IChannel channel,
        RabbitMqOptions options,
        CancellationToken cancellationToken = default)
    {
        await DeclareExchangeAsync(channel, cancellationToken);
        await channel.ExchangeDeclareAsync(RetryExchange, ExchangeType.Topic, durable: true, cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Topic, durable: true, cancellationToken: cancellationToken);

        // Main queue. Nothing in the normal flow dead-letters straight here, but configuring the
        // DLX means a nack without requeue still ends up in the DLQ rather than being lost.
        await channel.QueueDeclareAsync(
            ProductCreatedQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = DeadLetterExchange,
                ["x-dead-letter-routing-key"] = ProductCreatedRoutingKey
            },
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(ProductCreatedQueue, Exchange, ProductCreatedRoutingKey, cancellationToken: cancellationToken);

        // Retry ladder: each queue parks the message for its TTL, then dead-letters it back onto
        // the main exchange so it is consumed again after the delay.
        for (var attempt = 1; attempt <= options.RetryDelaysSeconds.Length; attempt++)
        {
            await channel.QueueDeclareAsync(
                RetryQueue(attempt),
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-message-ttl"] = options.RetryDelaysSeconds[attempt - 1] * 1000,
                    ["x-dead-letter-exchange"] = Exchange,
                    ["x-dead-letter-routing-key"] = ProductCreatedRoutingKey
                },
                cancellationToken: cancellationToken);
            await channel.QueueBindAsync(RetryQueue(attempt), RetryExchange, RetryRoutingKey(attempt), cancellationToken: cancellationToken);
        }

        await channel.QueueDeclareAsync(DeadLetterQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(DeadLetterQueue, DeadLetterExchange, ProductCreatedRoutingKey, cancellationToken: cancellationToken);
    }
}
