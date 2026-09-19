using DotnetApiSample.Application.Abstractions;
using DotnetApiSample.Infrastructure.Messaging;
using DotnetApiSample.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotnetApiSample.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

        services.AddSingleton<IDbConnectionFactory>(new NpgsqlConnectionFactory(connectionString));
        services.AddScoped<IProductRepository, DapperProductRepository>();

        var rabbitMq = configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()
            ?? new RabbitMqOptions();

        services.AddSingleton(rabbitMq);
        services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

        return services;
    }

    /// <summary>
    /// Registers the consumer side. Kept separate from <see cref="AddInfrastructure"/> so only the
    /// Worker consumes — the Api publishes and never competes for its own messages.
    /// </summary>
    public static IServiceCollection AddProductCreatedConsumer(this IServiceCollection services)
    {
        // Singleton, not scoped: the handler holds no per-message state and opens its own
        // connection per call. A scoped registration would be rejected at host startup because
        // the hosted service is a singleton.
        services.AddSingleton<IProductCreatedHandler, AuditProductCreatedHandler>();
        services.AddHostedService<ProductCreatedConsumer>();

        return services;
    }
}
