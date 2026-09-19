using DotnetApiSample.Application.Abstractions;

namespace DotnetApiSample.UnitTests.Fakes;

internal sealed class FakeEventPublisher : IEventPublisher
{
    public List<(object Message, string RoutingKey)> Published { get; } = [];

    public Task PublishAsync<TMessage>(TMessage message, string routingKey, CancellationToken cancellationToken)
        where TMessage : class
    {
        Published.Add((message, routingKey));

        return Task.CompletedTask;
    }
}
