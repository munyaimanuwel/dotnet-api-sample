namespace DotnetApiSample.Application.Abstractions;

public interface IEventPublisher
{
    Task PublishAsync<TMessage>(TMessage message, string routingKey, CancellationToken cancellationToken)
        where TMessage : class;
}
