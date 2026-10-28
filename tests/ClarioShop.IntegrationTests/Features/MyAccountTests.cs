using System.Net;
using System.Net.Http.Json;
using ClarioShop.IntegrationTests.Infrastructure;

namespace ClarioShop.IntegrationTests.Features;

[Collection(ApiCollection.Name)]
public sealed class MyAccountTests(ClarioApiFactory api)
{
    private const string Phone = "+8801712345678";
    private const string OtherPhone = "+8801812345678";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetMyProfile_SignedInShopper_ReturnsOwnCustomer()
    {
        var tenant = await api.CreateTenantAsync("me-get");
        using var shopper = await ShopperAsync(tenant, "01712-345678");

        var me = await shopper.GetFromJsonAsync<ProfileResponse>("/api/customers/me", Cancel);

        Assert.Equal((Phone, null, 0), (me!.Phone, me.Name, me.Addresses.Count));
    }

    [Fact]
    public async Task UpdateMyProfile_NameAndEmail_ArePersisted()
    {
        var tenant = await api.CreateTenantAsync("me-upd");
        using var shopper = await ShopperAsync(tenant, Phone);

        var response = await shopper.PutAsJsonAsync("/api/customers/me", new { name = "Rahim Uddin", email = "Rahim@Example.com" }, Cancel);

        await response.EnsureSuccessWithBodyAsync();
        var me = await shopper.GetFromJsonAsync<ProfileResponse>("/api/customers/me", Cancel);
        Assert.Equal(("Rahim Uddin", "rahim@example.com", Phone), (me!.Name, me.Email, me.Phone));
    }

    [Fact]
    public async Task MyAddresses_AddUpdateRemove_KeepsOneDefault()
    {
        var tenant = await api.CreateTenantAsync("me-addr");
        using var shopper = await ShopperAsync(tenant, Phone);
        var home = (await SendAsync(shopper, HttpMethod.Post, "/api/customers/me/addresses", Address("Home"))).Addresses.Single();
        var office = (await SendAsync(shopper, HttpMethod.Post, "/api/customers/me/addresses", Address("Office"))).Addresses.Single(a => a.Label == "Office");

        var updated = await SendAsync(shopper, HttpMethod.Put, $"/api/customers/me/addresses/{office.Id}", Address("Office", line1: "Level 7, Gulshan Avenue", makeDefault: true));
        var removed = await SendAsync(shopper, HttpMethod.Delete, $"/api/customers/me/addresses/{office.Id}", body: null);

        Assert.Equal(("Office", "Level 7, Gulshan Avenue", true), (updated.Addresses[0].Label, updated.Addresses[0].Line1, updated.Addresses[0].IsDefault));
        Assert.False(Assert.Single(updated.Addresses, a => a.Id == home.Id).IsDefault);
        Assert.Equal((home.Id, true), (Assert.Single(removed.Addresses).Id, removed.Addresses[0].IsDefault));
    }

    [Fact]
    public async Task UpdateMyAddress_AnotherShoppersAddress_ReturnsNotFoundAndChangesNothing()
    {
        var tenant = await api.CreateTenantAsync("me-iso");
        using var owner = await ShopperAsync(tenant, Phone);
        using var intruder = await ShopperAsync(tenant, OtherPhone);
        var address = (await SendAsync(owner, HttpMethod.Post, "/api/customers/me/addresses", Address("Home"))).Addresses.Single();

        var update = await intruder.PutAsJsonAsync($"/api/customers/me/addresses/{address.Id}", Address("Mine now"), Cancel);
        var remove = await intruder.DeleteAsync($"/api/customers/me/addresses/{address.Id}", Cancel);

        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound), (update.StatusCode, remove.StatusCode));
        var mine = await owner.GetFromJsonAsync<ProfileResponse>("/api/customers/me", Cancel);
        Assert.Equal("Home", Assert.Single(mine!.Addresses).Label);
        Assert.Empty((await intruder.GetFromJsonAsync<ProfileResponse>("/api/customers/me", Cancel))!.Addresses);
    }

    [Fact]
    public async Task GetMyProfile_Anonymous_ReturnsUnauthorized()
    {
        var tenant = await api.CreateTenantAsync("me-anon");
        using var guest = api.CreateTenantClient(tenant.Identifier);

        var response = await guest.GetAsync("/api/customers/me", Cancel);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMyProfile_StaffToken_ReturnsForbidden()
    {
        var tenant = await api.CreateTenantAsync("me-staff");
        using var staff = api.CreateTenantClient(tenant.Identifier, await api.SignInAsync(tenant.Identifier, tenant.OwnerEmail, tenant.OwnerPassword));

        var response = await staff.GetAsync("/api/customers/me", Cancel);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMyProfile_ShopperTokenFromAnotherShop_IsRejected()
    {
        var home = await api.CreateTenantAsync("me-home");
        var other = await api.CreateTenantAsync("me-other");
        var token = await api.SignInShopperAsync(home.Identifier, Phone);
        using var elsewhere = api.CreateTenantClient(other.Identifier, token);

        var response = await elsewhere.GetAsync("/api/customers/me", Cancel);

        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
    }

    private async Task<HttpClient> ShopperAsync(TestTenant tenant, string phone) =>
        api.CreateTenantClient(tenant.Identifier, await api.SignInShopperAsync(tenant.Identifier, phone));

    private static object Address(string label, string line1 = "House 12, Road 5", bool makeDefault = false) => new
    {
        label,
        recipientName = "Rahim Uddin",
        recipientPhone = "01712-345678",
        line1,
        area = "Dhanmondi",
        city = "Dhaka",
        postalCode = "1205",
        makeDefault,
    };

    private static async Task<ProfileResponse> SendAsync(HttpClient client, HttpMethod method, string url, object? body)
    {
        using var request = new HttpRequestMessage(method, url) { Content = body is null ? null : JsonContent.Create(body) };
        var response = await client.SendAsync(request, Cancel);
        await response.EnsureSuccessWithBodyAsync();
        return (await response.Content.ReadFromJsonAsync<ProfileResponse>(Cancel))!;
    }

    private sealed record ProfileResponse(Guid Id, string Phone, string? Name, string? Email, IReadOnlyList<AddressResponse> Addresses);

    private sealed record AddressResponse(Guid Id, string? Label, string Line1, bool IsDefault);
}
