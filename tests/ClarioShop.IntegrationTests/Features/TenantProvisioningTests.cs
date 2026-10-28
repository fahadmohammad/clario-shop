using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ClarioShop.IntegrationTests.Infrastructure;

namespace ClarioShop.IntegrationTests.Features;

[Collection(ApiCollection.Name)]
public sealed class TenantProvisioningTests(ClarioApiFactory api)
{
    [Fact]
    public async Task CreateTenant_NewShop_OwnerCanSignInWithEveryPermission()
    {
        var tenant = await api.CreateTenantAsync("prov");

        var token = await api.SignInAsync(tenant.Identifier, tenant.OwnerEmail, tenant.OwnerPassword);
        using var client = api.CreateTenantClient(tenant.Identifier, token);
        var me = await client.GetFromJsonAsync<MeResponse>("/api/identity/me", TestContext.Current.CancellationToken);

        Assert.Equal(tenant.OwnerEmail, me!.Email);
        Assert.Contains("catalog.products.edit", me.Permissions);
        Assert.Contains("identity.users.manage", me.Permissions);
    }

    [Fact]
    public async Task CreateTenant_WithoutPlatformKey_ReturnsUnauthorized()
    {
        using var client = api.CreateClient();

        var response = await client.GetAsync("/api/platform/tenants", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateTenant_DuplicateIdentifier_ReturnsConflict()
    {
        var tenant = await api.CreateTenantAsync("dup");
        using var platform = api.CreatePlatformClient();

        var response = await platform.PostAsJsonAsync("/api/platform/tenants", new
        {
            identifier = tenant.Identifier,
            name = "Again",
            hosts = new[] { $"other-{tenant.Identifier}.clarioshop.test" },
            ownerEmail = "someone@else.test",
            ownerName = "Someone",
            ownerPassword = "Owner-pass-123",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Request_UnknownTenant_ReturnsNotFound()
    {
        using var client = api.CreateTenantClient("no-such-shop");

        var response = await client.GetAsync("/api/catalog/products", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SignIn_WrongPassword_ReturnsInvalidGrant()
    {
        var tenant = await api.CreateTenantAsync("pwd");
        using var client = api.CreateTenantClient(tenant.Identifier);

        var response = await client.PostAsync("/api/identity/token", ClarioApiFactory.PasswordGrant(tenant.OwnerEmail, "Wrong-pass-999"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_grant", (await response.Content.ReadFromJsonAsync<OAuthError>(TestContext.Current.CancellationToken))!.Error);
    }

    [Fact]
    public async Task SignIn_TooManyAttempts_IsRateLimited()
    {
        var tenant = await api.CreateTenantAsync("rate");
        using var client = api.CreateTenantClient(tenant.Identifier);
        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt < 11; attempt++)
        {
            var response = await client.PostAsync("/api/identity/token", ClarioApiFactory.PasswordGrant(tenant.OwnerEmail, "Wrong-pass-999"), TestContext.Current.CancellationToken);
            statuses.Add(response.StatusCode);
        }

        Assert.All(statuses.Take(10), status => Assert.Equal(HttpStatusCode.BadRequest, status));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }

    private sealed record OAuthError([property: JsonPropertyName("error")] string Error);

    private sealed record MeResponse(Guid Id, string Email, string FullName, IReadOnlyList<string> Permissions);
}
