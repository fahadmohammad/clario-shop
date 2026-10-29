using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ClarioShop.Catalog.Contracts.Events;
using ClarioShop.Catalog.Persistence;
using ClarioShop.Customers.Contracts.Events;
using ClarioShop.Customers.Persistence;
using ClarioShop.Infrastructure.Email;
using ClarioShop.Infrastructure.Messaging;
using ClarioShop.Infrastructure.Sms;
using ClarioShop.Infrastructure.Tenancy;
using ClarioShop.Inventory.Contracts.Events;
using ClarioShop.Inventory.Persistence;
using ClarioShop.Ordering.Contracts.Events;
using ClarioShop.Ordering.Persistence;
using ClarioShop.Payments.Contracts.Events;
using ClarioShop.Payments.Gateways;
using ClarioShop.Payments.Persistence;
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

    public RecordingSmsSender Sms { get; } = new();

    public RecordingEmailSender Email { get; } = new();

    internal FakePaymentGateway Gateway { get; } = new();

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

    public async Task<string> SignInAsync(string tenant, string email, string password) =>
        (await SignInWithTokensAsync(tenant, email, password)).AccessToken;

    /// <summary>Password grant against the token endpoint; returns the access and refresh tokens.</summary>
    public async Task<TokenResponse> SignInWithTokensAsync(string tenant, string email, string password)
    {
        using var client = CreateTenantClient(tenant);
        var response = await client.PostAsync("/api/identity/token", PasswordGrant(email, password));
        await response.EnsureSuccessWithBodyAsync();
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
    }

    /// <summary>Phone OTP sign-in with the code the recording SMS sender captured; returns the shopper's access token.</summary>
    public async Task<string> SignInShopperAsync(string tenant, string phone)
    {
        using var client = CreateTenantClient(tenant);
        var sent = await client.PostAsJsonAsync("/api/identity/otp", new { phone });
        await sent.EnsureSuccessWithBodyAsync();
        var normalized = (await sent.Content.ReadFromJsonAsync<OtpSentResponse>())!.Phone;

        var response = await client.PostAsync("/api/identity/token", OtpGrant(phone, Sms.LatestCodeTo(normalized)));
        await response.EnsureSuccessWithBodyAsync();
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken;
    }

    public static FormUrlEncodedContent PasswordGrant(string email, string password) => new(new Dictionary<string, string>
    {
        ["grant_type"] = "password",
        ["username"] = email,
        ["password"] = password,
    });

    public static FormUrlEncodedContent OtpGrant(string phone, string code) => new(new Dictionary<string, string>
    {
        ["grant_type"] = "urn:clarioshop:params:oauth:grant-type:otp",
        ["phone"] = phone,
        ["code"] = code,
    });

    public static FormUrlEncodedContent RefreshTokenGrant(string refreshToken) => new(new Dictionary<string, string>
    {
        ["grant_type"] = "refresh_token",
        ["refresh_token"] = refreshToken,
    });

    /// <summary>A DI scope bound to the tenant, for calling module contracts the way another module would.</summary>
    public async Task<AsyncServiceScope> CreateTenantScopeAsync(string identifier)
    {
        TenantInfo? tenant;
        await using (var lookup = Services.CreateAsyncScope())
        {
            tenant = await lookup.ServiceProvider.GetRequiredService<ITenantStore>().FindByIdentifierAsync(identifier, CancellationToken.None);
        }

        return Services.GetRequiredService<TenantScopeFactory>().CreateScope(tenant ?? throw new InvalidOperationException($"Tenant {identifier} not found."));
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await DropTestDatabasesAsync();
        await postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var platformDb = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { ConnectionIdleLifetime = 2, ConnectionPruningInterval = 1, Database = $"clarioshop_platform_{runId}" }.ConnectionString;

        builder.UseEnvironment("Testing");
        // Tenant connection strings inherit these pool settings; idle connections from concurrent tests close quickly.
        var server = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { ConnectionIdleLifetime = 2, ConnectionPruningInterval = 1 }.ConnectionString;
        builder.UseSetting("ConnectionStrings:postgres", server);
        builder.UseSetting("ConnectionStrings:platformdb", platformDb);
        builder.UseSetting("Platform:ApiKey", PlatformKey);
        builder.UseSetting("Platform:MigrateOnStartup", "true");
        builder.UseSetting("Platform:DemoTenant:Enabled", "false");
        builder.UseSetting("Tenancy:AllowTenantHeader", "true");
        // A rotated refresh token is rejected at once, so the tests can see rotation without waiting.
        builder.UseSetting("Auth:RefreshTokenReuseLeeway", "00:00:00");
        builder.UseSetting("Messaging:OutboxPollInterval", "00:00:00.200");
        // Tests run the notification retry job themselves, at chosen times; the timer must not race them.
        builder.UseSetting("Notifications:RetryInterval", "01:00:00");
        // Tests run the drift check through its endpoint; a nightly run during CI must not correct their stock first.
        builder.UseSetting("Inventory:DriftCheckEnabled", "false");
        // Low enough that a test reaches it quickly; every test uses its own shop, so the counts never collide.
        builder.UseSetting("Auth:Otp:RequestsPerAddress", "5");

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(Events);
            services.AddSingleton<ISmsSender>(Sms);
            services.AddSingleton<IEmailSender>(Email);
            services.AddScoped<IPaymentGateway>(_ => Gateway);
            services.AddIntegrationEventHandler<ProductCreated, RecordingProductCreatedHandler, CatalogDbContext>("tests");
            services.AddIntegrationEventHandler<VariantAdded, RecordingHandler<VariantAdded>, CatalogDbContext>("tests");
            services.AddIntegrationEventHandler<ProductPriceChanged, RecordingHandler<ProductPriceChanged>, CatalogDbContext>("tests");
            services.AddIntegrationEventHandler<ProductArchived, RecordingHandler<ProductArchived>, CatalogDbContext>("tests");
            services.AddIntegrationEventHandler<CustomerCreated, RecordingCustomerCreatedHandler, CustomersDbContext>("tests");
            services.AddIntegrationEventHandler<OrderPlaced, RecordingOrderPlacedHandler, OrderingDbContext>("tests");
            services.AddIntegrationEventHandler<OrderConfirmed, RecordingHandler<OrderConfirmed>, OrderingDbContext>("tests");
            services.AddIntegrationEventHandler<OrderCancelled, RecordingHandler<OrderCancelled>, OrderingDbContext>("tests");
            services.AddIntegrationEventHandler<OrderShipped, RecordingHandler<OrderShipped>, OrderingDbContext>("tests");
            services.AddIntegrationEventHandler<OrderDelivered, RecordingHandler<OrderDelivered>, OrderingDbContext>("tests");
            services.AddIntegrationEventHandler<PaymentSucceeded, RecordingHandler<PaymentSucceeded>, PaymentsDbContext>("tests");
            services.AddIntegrationEventHandler<PaymentFailed, RecordingHandler<PaymentFailed>, PaymentsDbContext>("tests");
            services.AddIntegrationEventHandler<StockWentNegative, RecordingHandler<StockWentNegative>, InventoryDbContext>("tests");
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
}

public sealed record TokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);

public sealed record OtpSentResponse(string Phone, int ExpiresInSeconds);

public sealed record TestTenant(string Identifier, string OwnerEmail, string OwnerPassword);

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ClarioApiFactory>
{
    public const string Name = "api";
}
