using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ClarioShop.IntegrationTests.Infrastructure;
using ClarioShop.Payments.Gateways;
using ClarioShop.Payments.Gateways.SslCommerz;
using ClarioShop.Settings.Contracts;
using ClarioShop.Settings.Domain;
using ClarioShop.Settings.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClarioShop.IntegrationTests.Features;

[Collection(ApiCollection.Name)]
public sealed class PaymentGatewayTests(ClarioApiFactory api)
{
    private const string Password = "clario0live@ssl-Secret";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task UpdateSslCommerz_FirstSave_ReturnsAndListsAccountWithoutPassword()
    {
        var tenant = await api.CreateTenantAsync("pg-save");
        using var owner = await OwnerClientAsync(tenant);

        var response = await owner.PutAsJsonAsync("/api/settings/payment-gateways/sslcommerz", new SslCommerzBody("clario0live", Password, UseSandbox: true, IsEnabled: true), Cancel);
        await response.EnsureSuccessWithBodyAsync();
        var saved = await response.Content.ReadAsStringAsync(Cancel);
        var listed = await owner.GetStringAsync("/api/settings/payment-gateways", Cancel);
        var accounts = await owner.GetFromJsonAsync<List<GatewayResponse>>("/api/settings/payment-gateways", Cancel);

        var account = Assert.Single(accounts!);
        Assert.Equal(("SslCommerz", "clario0live", true, true), (account.Gateway, account.MerchantId, account.UseSandbox, account.IsEnabled));
        Assert.DoesNotContain(Password, saved);
        Assert.DoesNotContain(Password, listed);
    }

    [Fact]
    public async Task UpdateSslCommerz_FirstSaveWithoutPassword_ReturnsValidationProblem()
    {
        var tenant = await api.CreateTenantAsync("pg-nopass");
        using var owner = await OwnerClientAsync(tenant);

        var response = await owner.PutAsJsonAsync("/api/settings/payment-gateways/sslcommerz", new SslCommerzBody("clario0live", null, true, true), Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("storePassword", await response.Content.ReadAsStringAsync(Cancel));
        Assert.Null(await CredentialsAsync(tenant));
    }

    [Theory]
    [InlineData("", Password)]
    [InlineData("store id", Password)]
    [InlineData("clario0live", "")]
    public async Task UpdateSslCommerz_InvalidInput_ReturnsValidationProblem(string storeId, string password)
    {
        var tenant = await api.CreateTenantAsync("pg-invalid");
        using var owner = await OwnerClientAsync(tenant);

        var response = await owner.PutAsJsonAsync("/api/settings/payment-gateways/sslcommerz", new SslCommerzBody(storeId, password, true, true), Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateSslCommerz_Saved_StoresPasswordEncryptedAndContractDecryptsIt()
    {
        var tenant = await api.CreateTenantAsync("pg-enc");
        using var owner = await OwnerClientAsync(tenant);

        await SaveAsync(owner, new SslCommerzBody("clario0live", Password, UseSandbox: false, IsEnabled: true));
        var stored = await StoredSecretAsync(tenant);
        var credentials = await CredentialsAsync(tenant);

        Assert.DoesNotContain(Password, stored);
        Assert.Equal(new SslCommerzCredentials("clario0live", Password, UseSandbox: false), credentials);
        Assert.DoesNotContain(Password, credentials!.ToString());
    }

    [Fact]
    public async Task UpdateSslCommerz_WithoutPassword_KeepsSavedPassword()
    {
        var tenant = await api.CreateTenantAsync("pg-keep");
        using var owner = await OwnerClientAsync(tenant);
        await SaveAsync(owner, new SslCommerzBody("clario0test", Password, UseSandbox: true, IsEnabled: true));

        await SaveAsync(owner, new SslCommerzBody("clario0live", null, UseSandbox: false, IsEnabled: true));
        var credentials = await CredentialsAsync(tenant);

        Assert.Equal(new SslCommerzCredentials("clario0live", Password, UseSandbox: false), credentials);
    }

    [Fact]
    public async Task UpdateSslCommerz_NewPassword_ReplacesSavedPassword()
    {
        var tenant = await api.CreateTenantAsync("pg-replace");
        using var owner = await OwnerClientAsync(tenant);
        await SaveAsync(owner, new SslCommerzBody("clario0live", Password, true, true));

        await SaveAsync(owner, new SslCommerzBody("clario0live", "a-new-secret", true, true));
        var credentials = await CredentialsAsync(tenant);

        Assert.Equal("a-new-secret", credentials!.StorePassword);
    }

    [Fact]
    public async Task GetSslCommerz_AccountSwitchedOff_ReturnsNull()
    {
        var tenant = await api.CreateTenantAsync("pg-off");
        using var owner = await OwnerClientAsync(tenant);
        await SaveAsync(owner, new SslCommerzBody("clario0live", Password, true, true));

        await SaveAsync(owner, new SslCommerzBody("clario0live", null, true, IsEnabled: false));
        var accounts = await owner.GetFromJsonAsync<List<GatewayResponse>>("/api/settings/payment-gateways", Cancel);

        Assert.Null(await CredentialsAsync(tenant));
        Assert.False(Assert.Single(accounts!).IsEnabled);
    }

    [Fact]
    public async Task ProtectedSecret_CopiedToAnotherTenant_CannotBeDecryptedThere()
    {
        var owning = await api.CreateTenantAsync("pg-own");
        var other = await api.CreateTenantAsync("pg-other");
        using (var owner = await OwnerClientAsync(owning))
        {
            await SaveAsync(owner, new SslCommerzBody("clario0live", Password, true, true));
        }

        using (var owner = await OwnerClientAsync(other))
        {
            await SaveAsync(owner, new SslCommerzBody("other0live", "other-secret", true, true));
        }

        var copied = await StoredSecretAsync(owning);
        await using (var scope = await api.CreateTenantScopeAsync(other.Identifier))
        {
            await scope.ServiceProvider.GetRequiredService<SettingsDbContext>().PaymentGatewayAccounts
                .ExecuteUpdateAsync(set => set.SetProperty(account => account.ProtectedSecret, copied), Cancel);
        }

        await Assert.ThrowsAnyAsync<CryptographicException>(() => CredentialsAsync(other));
    }

    [Fact]
    public async Task PaymentGateways_WithoutSettingsPermission_AreForbidden()
    {
        var tenant = await api.CreateTenantAsync("pg-perm");
        using var owner = await OwnerClientAsync(tenant);
        (await owner.PostAsJsonAsync("/api/identity/users", new { email = "orders@shop.test", fullName = "Orders", password = "Staff-pass-123", roles = new[] { "Order Handler" } }, Cancel))
            .EnsureSuccessStatusCode();
        using var staff = api.CreateTenantClient(tenant.Identifier, await api.SignInAsync(tenant.Identifier, "orders@shop.test", "Staff-pass-123"));
        using var anonymous = api.CreateTenantClient(tenant.Identifier);

        var byStaff = await staff.PutAsJsonAsync("/api/settings/payment-gateways/sslcommerz", new SslCommerzBody("clario0live", Password, true, true), Cancel);
        var listedByStaff = await staff.GetAsync("/api/settings/payment-gateways", Cancel);
        var byAnonymous = await anonymous.GetAsync("/api/settings/payment-gateways", Cancel);

        Assert.Equal(HttpStatusCode.Forbidden, byStaff.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, listedByStaff.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, byAnonymous.StatusCode);
    }

    [Fact]
    public async Task PaymentGateway_StoreWithoutAccount_RefusesWithoutCallingGateway()
    {
        var tenant = await api.CreateTenantAsync("pg-none");
        await using var scope = await api.CreateTenantScopeAsync(tenant.Identifier);
        // The real SSLCommerz client; the tests' IPaymentGateway is a fake.
        var gateway = scope.ServiceProvider.GetRequiredService<SslCommerzGateway>();

        var session = await gateway.CreateSessionAsync(
            new GatewaySessionRequest(
                Guid.CreateVersion7().ToString("N"),
                100m,
                "BDT",
                "CS-000001",
                1,
                new GatewayCustomer("Rahim", "+8801712345678", "rahim@shop.test", "House 1", "Dhaka", null),
                new GatewayCallbacks(new Uri("https://shop.test/s"), new Uri("https://shop.test/f"), new Uri("https://shop.test/c"), new Uri("https://shop.test/i"))),
            Cancel);

        Assert.Equal(("SslCommerz", "payments.gateway_not_configured"), (gateway.Name, session.Error.Code));
    }

    private async Task SaveAsync(HttpClient owner, SslCommerzBody body) =>
        await (await owner.PutAsJsonAsync("/api/settings/payment-gateways/sslcommerz", body, Cancel)).EnsureSuccessWithBodyAsync();

    private async Task<SslCommerzCredentials?> CredentialsAsync(TestTenant tenant)
    {
        await using var scope = await api.CreateTenantScopeAsync(tenant.Identifier);
        return await scope.ServiceProvider.GetRequiredService<IPaymentGatewayCredentials>().GetSslCommerzAsync(Cancel);
    }

    private async Task<string> StoredSecretAsync(TestTenant tenant)
    {
        await using var scope = await api.CreateTenantScopeAsync(tenant.Identifier);
        return await scope.ServiceProvider.GetRequiredService<SettingsDbContext>().PaymentGatewayAccounts.AsNoTracking()
            .Where(account => account.Gateway == PaymentGateway.SslCommerz)
            .Select(account => account.ProtectedSecret)
            .SingleAsync(Cancel);
    }

    private async Task<HttpClient> OwnerClientAsync(TestTenant tenant) =>
        api.CreateTenantClient(tenant.Identifier, await api.SignInAsync(tenant.Identifier, tenant.OwnerEmail, tenant.OwnerPassword));

    private sealed record SslCommerzBody(string StoreId, string? StorePassword, bool UseSandbox, bool IsEnabled);

    private sealed record GatewayResponse(string Gateway, string MerchantId, bool UseSandbox, bool IsEnabled, DateTimeOffset UpdatedAt);
}
