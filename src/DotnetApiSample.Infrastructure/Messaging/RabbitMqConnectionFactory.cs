using RabbitMQ.Client;

namespace DotnetApiSample.Infrastructure.Messaging;

public static class RabbitMqConnectionFactory
{
    public static async Task<IConnection> ConnectAsync(RabbitMqOptions options, CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = options.Host,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
            VirtualHost = options.VirtualHost
        };

        return await factory.CreateConnectionAsync(cancellationToken);
    }
}
