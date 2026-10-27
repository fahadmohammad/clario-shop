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

        container = new PostgreSqlBuilder("postgres:17-alpine").Build();
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
