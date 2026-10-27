using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClarioShop.Catalog.Contracts.Events;
using ClarioShop.Catalog.Persistence;
using ClarioShop.Infrastructure.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ClarioShop.IntegrationTests.Infrastructure;

/// <summary>The real API on a real PostgreSQL server, with in-process messaging.</summary>
public sealed class ClarioApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string PlatformKey = "integration-tests-platform-key";

    private readonly PostgresServer postgres = new();
    private readonly string runId = Guid.NewGuid().ToString("N")[..8];

    public EventRecorder Events { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await postgres.StartAsync();
        _ = Server; // builds the host now, which migrates the platform database
    }

    public HttpClient CreatePlatformClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Platform-Key", PlatformKey);
        return client;
    }

    public HttpClient CreateTenantClient(string tenant, string? accessToken = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant", tenant);
        if (accessToken is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return client;
    }

    /// <summary>Provisions a fresh tenant with a unique identifier and returns its owner's credentials.</summary>
    public async Task<TestTenant> CreateTenantAsync(string prefix)
    {
        var identifier = $"{prefix}-{runId}-{Guid.NewGuid().ToString("N")[..6]}";
        var tenant = new TestTenant(identifier, $"owner@{identifier}.test", "Owner-pass-123");

        using var platform = CreatePlatformClient();
        var response = await platform.PostAsJsonAsync("/api/platform/tenants", new
        {
            identifier,
            name = $"Shop {identifier}",
            hosts = new[] { $"{identifier}.clarioshop.test" },
            ownerEmail = tenant.OwnerEmail,
            ownerName = "Test Owner",
            ownerPassword = tenant.OwnerPassword,
        });
        await response.EnsureSuccessWithBodyAsync();
        return tenant;
    }

    public async Task<string> SignInAsync(string tenant, string email, string password)
    {
        using var client = CreateTenantClient(tenant);
        var response = await client.PostAsJsonAsync("/api/identity/token", new { email, password });
        await response.EnsureSuccessWithBodyAsync();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return token!.AccessToken;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await DropTestDatabasesAsync();
        await postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var platformDb = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Database = $"clarioshop_platform_{runId}" }.ConnectionString;

        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:postgres", postgres.ConnectionString);
        builder.UseSetting("ConnectionStrings:platformdb", platformDb);
        builder.UseSetting("Platform:ApiKey", PlatformKey);
        builder.UseSetting("Platform:MigrateOnStartup", "true");
        builder.UseSetting("Platform:DemoTenant:Enabled", "false");
        builder.UseSetting("Tenancy:AllowTenantHeader", "true");
        builder.UseSetting("Auth:SigningKey", Convert.ToBase64String(Guid.NewGuid().ToByteArray().Concat(Guid.NewGuid().ToByteArray()).ToArray()));
        builder.UseSetting("Messaging:OutboxPollInterval", "00:00:00.200");

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(Events);
            services.AddIntegrationEventHandler<ProductCreated, RecordingProductCreatedHandler, CatalogDbContext>("tests");
        });
    }

    private async Task DropTestDatabasesAsync()
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync();

        var names = new List<string>();
        await using (var list = new NpgsqlCommand("SELECT datname FROM pg_database WHERE datname LIKE @pattern", connection))
        {
            list.Parameters.AddWithValue("pattern", $"clarioshop_%{runId}%");
            await using var reader = await list.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                names.Add(reader.GetString(0));
            }
        }

        foreach (var name in names)
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", connection);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private sealed record TokenResponse(string AccessToken);
}

public sealed record TestTenant(string Identifier, string OwnerEmail, string OwnerPassword);

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ClarioApiFactory>
{
    public const string Name = "api";
}
