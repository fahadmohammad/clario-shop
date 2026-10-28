using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ClarioShop.IntegrationTests.Infrastructure;

namespace ClarioShop.IntegrationTests.Features;

[Collection(ApiCollection.Name)]
public sealed class TokenServerTests(ClarioApiFactory api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PasswordGrant_ValidCredentials_ReturnsShortLivedAccessTokenAndRefreshToken()
    {
        var tenant = await api.CreateTenantAsync("tok");

        var tokens = await api.SignInWithTokensAsync(tenant.Identifier, tenant.OwnerEmail, tenant.OwnerPassword);

        Assert.Equal("Bearer", tokens.TokenType);
        Assert.InRange(tokens.ExpiresIn, 14 * 60, 15 * 60);
        Assert.False(string.IsNullOrEmpty(tokens.RefreshToken));
    }

    [Fact]
    public async Task RefreshTokenGrant_ValidToken_ReturnsNewTokensThatWork()
    {
        var tenant = await api.CreateTenantAsync("tok");
        var first = await api.SignInWithTokensAsync(tenant.Identifier, tenant.OwnerEmail, tenant.OwnerPassword);
        using var client = api.CreateTenantClient(tenant.Identifier);

        var response = await client.PostAsync("/api/identity/token", ClarioApiFactory.RefreshTokenGrant(first.RefreshToken!), Cancel);

        await response.EnsureSuccessWithBodyAsync();
        var refreshed = (await response.Content.ReadFromJsonAsync<TokenResponse>(Cancel))!;
        Assert.NotEqual(first.RefreshToken, refreshed.RefreshToken);
        using var signedIn = api.CreateTenantClient(tenant.Identifier, refreshed.AccessToken);
        var me = await signedIn.GetFromJsonAsync<MeResponse>("/api/identity/me", Cancel);
        Assert.Equal((tenant.OwnerEmail, true), (me!.Email, me.Permissions.Contains("identity.users.manage")));
    }

    [Fact]
    public async Task RefreshTokenGrant_TokenAlreadyRotated_ReturnsInvalidGrant()
    {
        var tenant = await api.CreateTenantAsync("tok");
        var first = await api.SignInWithTokensAsync(tenant.Identifier, tenant.OwnerEmail, tenant.OwnerPassword);
        using var client = api.CreateTenantClient(tenant.Identifier);
        (await client.PostAsync("/api/identity/token", ClarioApiFactory.RefreshTokenGrant(first.RefreshToken!), Cancel)).EnsureSuccessStatusCode();

        var replay = await client.PostAsync("/api/identity/token", ClarioApiFactory.RefreshTokenGrant(first.RefreshToken!), Cancel);

        Assert.Equal((HttpStatusCode.BadRequest, "invalid_grant"), (replay.StatusCode, await ErrorCodeAsync(replay)));
    }

    [Fact]
    public async Task RefreshTokenGrant_TokenFromAnotherShop_ReturnsInvalidGrant()
    {
        var issuer = await api.CreateTenantAsync("tok");
        var other = await api.CreateTenantAsync("tok");
        var tokens = await api.SignInWithTokensAsync(issuer.Identifier, issuer.OwnerEmail, issuer.OwnerPassword);
        using var client = api.CreateTenantClient(other.Identifier);

        var response = await client.PostAsync("/api/identity/token", ClarioApiFactory.RefreshTokenGrant(tokens.RefreshToken!), Cancel);

        Assert.Equal((HttpStatusCode.BadRequest, "invalid_grant"), (response.StatusCode, await ErrorCodeAsync(response)));
    }

    [Fact]
    public async Task RefreshTokenGrant_MalformedToken_ReturnsInvalidGrant()
    {
        var tenant = await api.CreateTenantAsync("tok");
        using var client = api.CreateTenantClient(tenant.Identifier);

        var response = await client.PostAsync("/api/identity/token", ClarioApiFactory.RefreshTokenGrant("not-a-token"), Cancel);

        Assert.Equal((HttpStatusCode.BadRequest, "invalid_grant"), (response.StatusCode, await ErrorCodeAsync(response)));
    }

    [Fact]
    public async Task AccessToken_FromAnotherShop_ReturnsForbidden()
    {
        var issuer = await api.CreateTenantAsync("tok");
        var other = await api.CreateTenantAsync("tok");
        var token = await api.SignInAsync(issuer.Identifier, issuer.OwnerEmail, issuer.OwnerPassword);
        using var client = api.CreateTenantClient(other.Identifier, token);

        var response = await client.PostAsJsonAsync("/api/identity/users", new { email = "staff@other.test", fullName = "Staff", password = "Staff-pass-123", roles = new[] { "Owner" } }, Cancel);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TokenEndpoint_JsonBody_ReturnsInvalidRequest()
    {
        var tenant = await api.CreateTenantAsync("tok");
        using var client = api.CreateTenantClient(tenant.Identifier);

        var response = await client.PostAsJsonAsync("/api/identity/token", new { email = tenant.OwnerEmail, password = tenant.OwnerPassword }, Cancel);

        Assert.Equal((HttpStatusCode.BadRequest, "invalid_request"), (response.StatusCode, await ErrorCodeAsync(response)));
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<OAuthError>(Cancel))?.Error;

    private sealed record OAuthError([property: JsonPropertyName("error")] string Error);

    private sealed record MeResponse(Guid Id, string Email, string FullName, IReadOnlyList<string> Permissions);
}
