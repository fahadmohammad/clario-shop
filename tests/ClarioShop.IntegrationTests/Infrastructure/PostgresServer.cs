using Testcontainers.PostgreSql;

namespace ClarioShop.IntegrationTests.Infrastructure;

/// <summary>
/// A real PostgreSQL server: Testcontainers by default, or an existing server when
/// CLARIOSHOP_TEST_POSTGRES is set (e.g. "Host=localhost;Username=postgres;Password=postgres").
/// </summary>
public sealed class PostgresServer : IAsyncDisposable
{
    private const string ExternalServerVariable = "CLARIOSHOP_TEST_POSTGRES";

    private PostgreSqlContainer? container;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task StartAsync()
    {
        var external = Environment.GetEnvironmentVariable(ExternalServerVariable);
        if (!string.IsNullOrWhiteSpace(external))
        {
            ConnectionString = external;
            return;
        }

        // Every test tenant is its own database with its own connection pool; the default 100 connections run out.
        container = new PostgreSqlBuilder("postgres:17-alpine").WithCommand("-c", "max_connections=500").Build();
        await container.StartAsync();
        ConnectionString = container.GetConnectionString();
    }

    public async ValueTask DisposeAsync()
    {
        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }
}
