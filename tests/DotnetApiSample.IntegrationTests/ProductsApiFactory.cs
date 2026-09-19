using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;

namespace DotnetApiSample.IntegrationTests;

public sealed class ProductsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _postgres.StartAsync();
        await ApplySchemaAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        base.Dispose();
    }

    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            TRUNCATE products RESTART IDENTITY;
            INSERT INTO products (name, sku, price) VALUES
                ('Espresso Machine', 'ESP-001', 249.00),
                ('Burr Grinder',     'GRD-002',  89.50),
                ('Gooseneck Kettle', 'KTL-003',  45.00);
            """;
        await command.ExecuteNonQueryAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Host-level settings: these are applied before the app's WebApplicationBuilder reads
        // configuration, unlike ConfigureAppConfiguration which lands too late in the minimal
        // hosting model. "Testing" also keeps Development-only user-secrets out of the run.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
    }

    private async Task ApplySchemaAsync()
    {
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "db", "init.sql");
        var schema = await File.ReadAllTextAsync(schemaPath);

        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = schema;
        await command.ExecuteNonQueryAsync();
    }
}
