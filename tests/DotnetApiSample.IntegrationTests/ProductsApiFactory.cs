using DotnetApiSample.Infrastructure.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace DotnetApiSample.IntegrationTests;

public sealed class ProductsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Short delays so the retry ladder completes inside a test.</summary>
    private static readonly int[] TestRetryDelaysSeconds = [1, 1, 1];

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    // Credentials are set explicitly: the module's defaults are not guest/guest, and RabbitMQ's
    // built-in guest user is refused for any non-loopback connection.
    private const string BrokerUserName = "dotnet-api-sample";
    private const string BrokerPassword = "dotnet-api-sample";

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:3.13-alpine")
        .WithUsername(BrokerUserName)
        .WithPassword(BrokerPassword)
        .Build();

    public string PostgresConnectionString => _postgres.GetConnectionString();

    public RabbitMqOptions RabbitMqSettings { get; private set; } = new();

    async Task IAsyncLifetime.InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync());

        RabbitMqSettings = new RabbitMqOptions
        {
            Host = _rabbitMq.Hostname,
            Port = _rabbitMq.GetMappedPublicPort(5672),
            UserName = BrokerUserName,
            Password = BrokerPassword,
            VirtualHost = "/",
            RetryDelaysSeconds = TestRetryDelaysSeconds
        };

        await ApplySchemaAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _rabbitMq.DisposeAsync();
        await _postgres.DisposeAsync();
        base.Dispose();
    }

    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(PostgresConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            TRUNCATE products RESTART IDENTITY;
            TRUNCATE product_audit RESTART IDENTITY;
            INSERT INTO products (name, sku, price) VALUES
                ('Espresso Machine', 'ESP-001', 249.00),
                ('Burr Grinder',     'GRD-002',  89.50),
                ('Gooseneck Kettle', 'KTL-003',  45.00);
            """;
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Drains every queue in the topology so tests do not inherit each other's messages.</summary>
    public async Task ResetMessagingAsync()
    {
        await using var connection = await RabbitMqConnectionFactory.ConnectAsync(RabbitMqSettings, CancellationToken.None);
        await using var channel = await connection.CreateChannelAsync();

        await RabbitMqTopology.DeclareAsync(channel, RabbitMqSettings);

        await channel.QueuePurgeAsync(RabbitMqTopology.ProductCreatedQueue);
        for (var attempt = 1; attempt <= RabbitMqSettings.RetryDelaysSeconds.Length; attempt++)
        {
            await channel.QueuePurgeAsync(RabbitMqTopology.RetryQueue(attempt));
        }

        await channel.QueuePurgeAsync(RabbitMqTopology.DeadLetterQueue);
    }

    public async Task<long> CountAuditRowsAsync(int productId)
    {
        await using var connection = new NpgsqlConnection(PostgresConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM product_audit WHERE product_id = @id;";
        command.Parameters.AddWithValue("id", productId);

        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Host-level settings: these are applied before the app's WebApplicationBuilder reads
        // configuration, unlike ConfigureAppConfiguration which lands too late in the minimal
        // hosting model. "Testing" also keeps Development-only user-secrets out of the run.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Postgres", PostgresConnectionString);

        builder.UseSetting("RabbitMq:Host", RabbitMqSettings.Host);
        builder.UseSetting("RabbitMq:Port", RabbitMqSettings.Port.ToString());
        builder.UseSetting("RabbitMq:UserName", RabbitMqSettings.UserName);
        builder.UseSetting("RabbitMq:Password", RabbitMqSettings.Password);
        builder.UseSetting("RabbitMq:VirtualHost", RabbitMqSettings.VirtualHost);

        for (var index = 0; index < RabbitMqSettings.RetryDelaysSeconds.Length; index++)
        {
            builder.UseSetting($"RabbitMq:RetryDelaysSeconds:{index}", RabbitMqSettings.RetryDelaysSeconds[index].ToString());
        }
    }

    private async Task ApplySchemaAsync()
    {
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "db", "init.sql");
        var schema = await File.ReadAllTextAsync(schemaPath);

        await using var connection = new NpgsqlConnection(PostgresConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = schema;
        await command.ExecuteNonQueryAsync();
    }
}
