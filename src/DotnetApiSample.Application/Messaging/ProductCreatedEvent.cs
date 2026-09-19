namespace DotnetApiSample.Application.Messaging;

public sealed record ProductCreatedEvent(
    Guid EventId,
    int ProductId,
    string Sku,
    string Name,
    decimal Price,
    DateTimeOffset OccurredAt);
