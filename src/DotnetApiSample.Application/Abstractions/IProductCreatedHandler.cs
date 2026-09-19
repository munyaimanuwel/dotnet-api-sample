using DotnetApiSample.Application.Messaging;

namespace DotnetApiSample.Application.Abstractions;

public interface IProductCreatedHandler
{
    Task HandleAsync(ProductCreatedEvent @event, CancellationToken cancellationToken);
}
