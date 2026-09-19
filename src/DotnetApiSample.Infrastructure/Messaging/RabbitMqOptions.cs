namespace DotnetApiSample.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string UserName { get; set; } = "guest";

    public string Password { get; set; } = string.Empty;

    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Delay in seconds applied before each retry attempt. The number of entries is the
    /// retry cap: once every delay has been used the message is dead-lettered.
    /// </summary>
    public int[] RetryDelaysSeconds { get; set; } = [5, 15, 45];
}
