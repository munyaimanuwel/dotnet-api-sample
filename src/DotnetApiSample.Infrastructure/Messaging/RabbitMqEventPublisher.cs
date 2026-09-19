using System.Text.Json;
using DotnetApiSample.Application.Abstractions;
using RabbitMQ.Client;

namespace DotnetApiSample.Infrastructure.Messaging;

internal sealed class RabbitMqEventPublisher : IEventPublisher, IAsyncDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly RabbitMqOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqEventPublisher(RabbitMqOptions options)
    {
        _options = options;
    }

    public async Task PublishAsync<TMessage>(TMessage message, string routingKey, CancellationToken cancellationToken)
        where TMessage : class
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(message, SerializerOptions);
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = Guid.NewGuid().ToString()
        };

        // IChannel is not thread-safe, so publishing is serialised.
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var channel = await GetChannelAsync(cancellationToken);
            await channel.BasicPublishAsync(
                RabbitMqTopology.Exchange,
                routingKey,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        _connection = await RabbitMqConnectionFactory.ConnectAsync(_options, cancellationToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await RabbitMqTopology.DeclareExchangeAsync(_channel, cancellationToken);

        return _channel;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _gate.Dispose();
    }
}
