using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ClarioShop.Customers.Contracts;
using ClarioShop.Identity.Persistence;
using ClarioShop.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClarioShop.IntegrationTests.Features;

[Collection(ApiCollection.Name)]
public sealed class OtpSignInTests(ClarioApiFactory api)
{
    private const string Phone = "+8801712345678";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RequestOtp_ValidPhone_SendsSixDigitCodeToNormalizedNumber()
    {
        var tenant = await api.CreateTenantAsync("otp");
        using var client = api.CreateTenantClient(tenant.Identifier);

        var response = await client.PostAsJsonAsync("/api/identity/otp", new { phone = "01712-345678" }, Cancel);

        await response.EnsureSuccessWithBodyAsync();
        var sent = (await response.Content.ReadFromJsonAsync<OtpSent>(Cancel))!;
        Assert.Equal((HttpStatusCode.Accepted, Phone, 180), (response.StatusCode, sent.Phone, sent.ExpiresInSeconds));
        Assert.Matches("^[0-9]{6}$", api.Sms.LatestCodeTo(Phone));
    }

    [Fact]
    public async Task RequestOtp_NotABangladeshiMobile_ReturnsValidationProblem()
    {
        var tenant = await api.CreateTenantAsync("otp");
        using var client = api.CreateTenantClient(tenant.Identifier);

        var response = await client.PostAsJsonAsync("/api/identity/otp", new { phone = "12345" }, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OtpGrant_CorrectCode_SignsInShopperLinkedToCustomer()
    {
        var tenant = await api.CreateTenantAsync("otp");

        var tokens = await SignInAsync(tenant.Identifier, "01712-345678");

        Assert.InRange(tokens.ExpiresIn, 14 * 60, 15 * 60);
        Assert.False(string.IsNullOrEmpty(tokens.RefreshToken));
        using var shopper = api.CreateTenantClient(tenant.Identifier, tokens.AccessToken);
        var me = (await shopper.GetFromJsonAsync<MeResponse>("/api/identity/me", Cancel))!;
        Assert.Equal((Phone, null, 0), (me.Phone, me.Email, me.Permissions.Count));
        await using var scope = await api.CreateTenantScopeAsync(tenant.Identifier);
        var customer = await scope.ServiceProvider.GetRequiredService<ICustomerDirectory>().FindOrCreateByPhoneAsync(Phone, null, Cancel);
        var linked = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Users
            .Where(user => user.Id == me.Id).Select(user => user.CustomerId).SingleAsync(Cancel);
        Assert.Equal(customer.Value.CustomerId, linked);
    }

    [Fact]
    public async Task OtpGrant_SamePhoneInAnotherFormat_SignsInSameAccount()
    {
        var tenant = await api.CreateTenantAsync("otp");
        var first = await SignInAsync(tenant.Identifier, "01712-345678");

        var second = await SignInAsync(tenant.Identifier, "8801712345678");

        Assert.Equal(await MeIdAsync(tenant.Identifier, first.AccessToken), await MeIdAsync(tenant.Identifier, second.AccessToken));
    }

    [Fact]
    public async Task OtpGrant_ShopperCannotUseStaffEndpoints_ReturnsForbidden()
    {
        var tenant = await api.CreateTenantAsync("otp");
        var tokens = await SignInAsync(tenant.Identifier, Phone);
        using var shopper = api.CreateTenantClient(tenant.Identifier, tokens.AccessToken);

        var response = await shopper.GetAsync("/api/customers", Cancel);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OtpGrant_WrongCode_ReturnsInvalidGrant()
    {
        var tenant = await api.CreateTenantAsync("otp");
        using var client = api.CreateTenantClient(tenant.Identifier);
        await RequestCodeAsync(client, Phone);

        var response = await client.PostAsync("/api/identity/token", ClarioApiFactory.OtpGrant(Phone, WrongCode(api.Sms.LatestCodeTo(Phone))), Cancel);

        Assert.Equal((HttpStatusCode.BadRequest, "invalid_grant"), (response.StatusCode, await ErrorCodeAsync(response)));
    }

    [Fact]
    public async Task OtpGrant_CodeAlreadyUsed_ReturnsInvalidGrant()
    {
        var tenant = await api.CreateTenantAsync("otp");
        using var client = api.CreateTenantClient(tenant.Identifier);
        await RequestCodeAsync(client, Phone);
        var code = api.Sms.LatestCodeTo(Phone);
        (await client.PostAsync("/api/identity/token", ClarioApiFactory.OtpGrant(Phone, code), Cancel)).EnsureSuccessStatusCode();

        var replay = await client.PostAsync("/api/identity/token", ClarioApiFactory.OtpGrant(Phone, code), Cancel);

        Assert.Equal((HttpStatusCode.BadRequest, "invalid_grant"), (replay.StatusCode, await ErrorCodeAsync(replay)));
    }

    [Fact]
    public async Task OtpGrant_CodeExpired_ReturnsInvalidGrant()
    {
        var tenant = await api.CreateTenantAsync("otp");
        using var client = api.CreateTenantClient(tenant.Identifier);
        await RequestCodeAsync(client, Phone);
        await using (var scope = await api.CreateTenantScopeAsync(tenant.Identifier))
        {
            // The code was sent more than 3 minutes ago.
            await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().OtpCodes
                .ExecuteUpdateAsync(setters => setters.SetProperty(otp => otp.ExpiresAt, DateTimeOffset.UtcNow.AddSeconds(-1)), Cancel);
        }

        var response = await client.PostAsync("/api/identity/token", ClarioApiFactory.OtpGrant(Phone, api.Sms.LatestCodeTo(Phone)), Cancel);

        Assert.Equal((HttpStatusCode.BadRequest, "invalid_grant"), (response.StatusCode, await ErrorCodeAsync(response)));
    }

    [Fact]
    public async Task OtpGrant_FiveWrongAttempts_RejectsCorrectCode()
    {
        var tenant = await api.CreateTenantAsync("otp");
        using var client = api.CreateTenantClient(tenant.Identifier);
        await RequestCodeAsync(client, Phone);
        var code = api.Sms.LatestCodeTo(Phone);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await client.PostAsync("/api/identity/token", ClarioApiFactory.OtpGrant(Phone, WrongCode(code)), Cancel);
        }

        var response = await client.PostAsync("/api/identity/token", ClarioApiFactory.OtpGrant(Phone, code), Cancel);

        Assert.Equal((HttpStatusCode.BadRequest, "invalid_grant"), (response.StatusCode, await ErrorCodeAsync(response)));
    }

    [Fact]
    public async Task OtpGrant_FourWrongAttempts_AcceptsCorrectCode()
    {
        var tenant = await api.CreateTenantAsync("otp");
        using var client = api.CreateTenantClient(tenant.Identifier);
        await RequestCodeAsync(client, Phone);
        var code = api.Sms.LatestCodeTo(Phone);
        for (var attempt = 0; attempt < 4; attempt++)
        {
            await client.PostAsync("/api/identity/token", ClarioApiFactory.OtpGrant(Phone, WrongCode(code)), Cancel);
        }

        var response = await client.PostAsync("/api/identity/token", ClarioApiFactory.OtpGrant(Phone, code), Cancel);

        await response.EnsureSuccessWithBodyAsync();
    }

    [Fact]
    public async Task OtpGrant_ConcurrentAttempts_NeverExceedAttemptLimit()
    {
        var tenant = await api.CreateTenantAsync("otp");
        using var client = api.CreateTenantClient(tenant.Identifier);
        await RequestCodeAsync(client, Phone);
        var code = api.Sms.LatestCodeTo(Phone);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PostAsync("/api/identity/token", ClarioApiFactory.OtpGrant(Phone, WrongCode(code)), Cancel)));

        await using var scope = await api.CreateTenantScopeAsync(tenant.Identifier);
        var attempts = await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().OtpCodes.Select(otp => otp.Attempts).SingleAsync(Cancel);
        Assert.Equal(5, attempts);
    }

    [Fact]
    public async Task OtpGrant_OlderCodeAfterNewOneWasSent_ReturnsInvalidGrant()
    {
        var tenant = await api.CreateTenantAsync("otp");
        using var client = api.CreateTenantClient(tenant.Identifier);
        await RequestCodeAsync(client, Phone);
        var older = api.Sms.LatestCodeTo(Phone);
        await RequestCodeAsync(client, Phone);
        if (api.Sms.LatestCodeTo(Phone) == older)
        {
            return; // one in a million: both codes are equal, so the older one is also the newest
        }

        var response = await client.PostAsync("/api/identity/token", ClarioApiFactory.OtpGrant(Phone, older), Cancel);

        Assert.Equal((HttpStatusCode.BadRequest, "invalid_grant"), (response.StatusCode, await ErrorCodeAsync(response)));
    }

    [Fact]
    public async Task RequestOtp_FourthCodeForPhoneInWindow_ReturnsTooManyRequests()
    {
        var tenant = await api.CreateTenantAsync("otp");
        using var client = api.CreateTenantClient(tenant.Identifier);
        for (var request = 0; request < 3; request++)
        {
            await RequestCodeAsync(client, Phone);
        }

        var response = await client.PostAsJsonAsync("/api/identity/otp", new { phone = "01712 345678" }, Cancel);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task RequestOtp_TooManyFromOneAddress_ReturnsTooManyRequests()
    {
        var tenant = await api.CreateTenantAsync("otp");
        using var client = api.CreateTenantClient(tenant.Identifier);
        for (var request = 0; request < 5; request++)
        {
            await RequestCodeAsync(client, $"+88017123456{request:D2}");
        }

        var response = await client.PostAsJsonAsync("/api/identity/otp", new { phone = "+8801712345699" }, Cancel);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task RequestOtp_LimitReachedInOneShop_OtherShopStillSends()
    {
        var limited = await api.CreateTenantAsync("otp");
        var other = await api.CreateTenantAsync("otp");
        using var limitedClient = api.CreateTenantClient(limited.Identifier);
        for (var request = 0; request < 3; request++)
        {
            await RequestCodeAsync(limitedClient, Phone);
        }

        using var otherClient = api.CreateTenantClient(other.Identifier);
        var response = await otherClient.PostAsJsonAsync("/api/identity/otp", new { phone = Phone }, Cancel);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task RefreshTokenGrant_Shopper_ReturnsNewTokens()
    {
        var tenant = await api.CreateTenantAsync("otp");
        var tokens = await SignInAsync(tenant.Identifier, Phone);
        using var client = api.CreateTenantClient(tenant.Identifier);

        var response = await client.PostAsync("/api/identity/token", ClarioApiFactory.RefreshTokenGrant(tokens.RefreshToken!), Cancel);

        await response.EnsureSuccessWithBodyAsync();
        var refreshed = (await response.Content.ReadFromJsonAsync<TokenResponse>(Cancel))!;
        Assert.Equal(await MeIdAsync(tenant.Identifier, tokens.AccessToken), await MeIdAsync(tenant.Identifier, refreshed.AccessToken));
    }

    private static async Task RequestCodeAsync(HttpClient client, string phone)
    {
        var response = await client.PostAsJsonAsync("/api/identity/otp", new { phone }, Cancel);
        await response.EnsureSuccessWithBodyAsync();
    }

    private static string WrongCode(string code) => code == "000000" ? "111111" : "000000";

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<OAuthError>(Cancel))?.Error;

    private async Task<TokenResponse> SignInAsync(string tenant, string phone)
    {
        using var client = api.CreateTenantClient(tenant);
        var sent = await client.PostAsJsonAsync("/api/identity/otp", new { phone }, Cancel);
        await sent.EnsureSuccessWithBodyAsync();
        var normalized = (await sent.Content.ReadFromJsonAsync<OtpSent>(Cancel))!.Phone;

        var response = await client.PostAsync("/api/identity/token", ClarioApiFactory.OtpGrant(phone, api.Sms.LatestCodeTo(normalized)), Cancel);
        await response.EnsureSuccessWithBodyAsync();
        return (await response.Content.ReadFromJsonAsync<TokenResponse>(Cancel))!;
    }

    private async Task<Guid> MeIdAsync(string tenant, string accessToken)
    {
        using var client = api.CreateTenantClient(tenant, accessToken);
        return (await client.GetFromJsonAsync<MeResponse>("/api/identity/me", Cancel))!.Id;
    }

    private sealed record OtpSent(string Phone, int ExpiresInSeconds);

    private sealed record OAuthError([property: JsonPropertyName("error")] string Error);

    private sealed record MeResponse(Guid Id, string? Email, string? Phone, string FullName, IReadOnlyList<string> Permissions);
}
