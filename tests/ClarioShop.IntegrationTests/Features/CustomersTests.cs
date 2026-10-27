using System.Net;
using System.Net.Http.Json;
using ClarioShop.Customers.Contracts;
using ClarioShop.Customers.Contracts.Events;
using ClarioShop.Customers.Persistence;
using ClarioShop.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ClarioShop.IntegrationTests.Features;

[Collection(ApiCollection.Name)]
public sealed class CustomersTests(ClarioApiFactory api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FindOrCreateByPhone_SameNumberInThreeFormats_ReturnsOneCustomer()
    {
        var tenant = await api.CreateTenantAsync("cust-fmt");

        var first = await FindOrCreateAsync(tenant, "01712-345678", "Rahim Uddin");
        var second = await FindOrCreateAsync(tenant, "+8801712345678", "Someone Else");
        var third = await FindOrCreateAsync(tenant, "8801712345678", null);

        Assert.Equal(first.CustomerId, second.CustomerId);
        Assert.Equal(first.CustomerId, third.CustomerId);
        Assert.Equal(("+8801712345678", "Rahim Uddin"), (third.Phone, third.Name));
    }

    [Fact]
    public async Task FindOrCreateByPhone_ManyConcurrentCallers_CreateOneCustomerAndOneEvent()
    {
        const int Callers = 12;
        var tenant = await api.CreateTenantAsync("cust-race");
        string[] formats = ["01812-345678", "+8801812345678", "8801812345678", "01812345678"];

        var attempts = Enumerable.Range(0, Callers).Select(async i =>
        {
            await using var scope = await api.CreateTenantScopeAsync(tenant.Identifier);
            var directory = scope.ServiceProvider.GetRequiredService<ICustomerDirectory>();
            return await directory.FindOrCreateByPhoneAsync(formats[i % formats.Length], $"Caller {i}", Cancel);
        });
        var results = await Task.WhenAll(attempts);

        Assert.All(results, result => Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null));
        Assert.Single(results.Select(result => result.Value.CustomerId).Distinct());
        await using var check = await api.CreateTenantScopeAsync(tenant.Identifier);
        var db = check.ServiceProvider.GetRequiredService<CustomersDbContext>();
        Assert.Equal(1, await db.Customers.CountAsync(Cancel));
        Assert.Equal(1, await db.OutboxMessages.CountAsync(m => m.Type.Contains(nameof(CustomerCreated)), Cancel));
    }

    [Fact]
    public async Task FindOrCreateByPhone_NewCustomer_PublishesCustomerCreated()
    {
        var tenant = await api.CreateTenantAsync("cust-evt");

        var customer = await FindOrCreateAsync(tenant, "01912 345 678", "Karima Begum");

        var created = await api.Events.WaitForAsync<CustomerCreated>(e => e.CustomerId == customer.CustomerId, TimeSpan.FromSeconds(15));
        Assert.Equal(("+8801912345678", "Karima Begum"), (created.Phone, created.Name));
    }

    [Fact]
    public async Task FindOrCreateByPhone_NotAMobileNumber_ReturnsValidationError()
    {
        var tenant = await api.CreateTenantAsync("cust-bad");
        await using var scope = await api.CreateTenantScopeAsync(tenant.Identifier);
        var directory = scope.ServiceProvider.GetRequiredService<ICustomerDirectory>();

        var result = await directory.FindOrCreateByPhoneAsync("02-9876543", "Landline", Cancel);

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error.Code);
    }

    [Fact]
    public async Task Get_ThroughContract_ReturnsAddressesDefaultFirst()
    {
        var tenant = await api.CreateTenantAsync("cust-get");
        using var staff = await OwnerClientAsync(tenant);
        var customer = await CreateCustomerAsync(staff, "01712-000111", "Rahim Uddin");
        await AddAddressAsync(staff, customer.Id, "Home", makeDefault: false);
        await AddAddressAsync(staff, customer.Id, "Office", makeDefault: true);
        await using var scope = await api.CreateTenantScopeAsync(tenant.Identifier);
        var directory = scope.ServiceProvider.GetRequiredService<ICustomerDirectory>();

        var info = await directory.GetAsync(customer.Id, Cancel);
        var missing = await directory.GetAsync(Guid.CreateVersion7(), Cancel);

        Assert.NotNull(info);
        Assert.Equal(["Office", "Home"], info.Addresses.Select(a => a.Label));
        Assert.True(info.Addresses[0].IsDefault);
        Assert.Null(missing);
    }

    [Fact]
    public async Task Create_PhoneAlreadyUsedInAnotherFormat_ReturnsConflict()
    {
        var tenant = await api.CreateTenantAsync("cust-dup");
        using var staff = await OwnerClientAsync(tenant);
        await CreateCustomerAsync(staff, "01712-345678", "Rahim Uddin");

        var response = await staff.PostAsJsonAsync("/api/customers", new { phone = "+880 1712 345678", name = "Duplicate" }, Cancel);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("""{ "phone": "12345" }""")]
    [InlineData("""{ "phone": "01712345678", "email": "not-an-email" }""")]
    public async Task Create_InvalidInput_ReturnsValidationProblem(string body)
    {
        var tenant = await api.CreateTenantAsync("cust-inv");
        using var staff = await OwnerClientAsync(tenant);

        var response = await staff.PostAsync("/api/customers", new StringContent(body, System.Text.Encoding.UTF8, "application/json"), Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("01712")]
    [InlineData("+8801712")]
    [InlineData("karima")]
    [InlineData("KARIMA@shop")]
    public async Task Search_ByNamePhoneFragmentOrEmail_FindsCustomer(string search)
    {
        var tenant = await api.CreateTenantAsync("cust-find");
        using var staff = await OwnerClientAsync(tenant);
        var karima = await CreateCustomerAsync(staff, "01712-345678", "Karima Begum", "karima@shop.test");
        await CreateCustomerAsync(staff, "01912-999888", "Rahim Uddin");

        var page = await staff.GetFromJsonAsync<CustomerPage>($"/api/customers?search={Uri.EscapeDataString(search)}", Cancel);

        var found = Assert.Single(page!.Items);
        Assert.Equal(karima.Id, found.Id);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task Search_NoFilter_ListsNewestFirst()
    {
        var tenant = await api.CreateTenantAsync("cust-list");
        using var staff = await OwnerClientAsync(tenant);
        var older = await CreateCustomerAsync(staff, "01712-111111", "Older");
        var newer = await CreateCustomerAsync(staff, "01712-222222", "Newer");
        await AddAddressAsync(staff, newer.Id, "Home", makeDefault: false);

        var page = await staff.GetFromJsonAsync<CustomerPage>("/api/customers?pageSize=10", Cancel);

        Assert.Equal([newer.Id, older.Id], page!.Items.Select(c => c.Id));
        Assert.Equal(1, page.Items[0].AddressCount);
    }

    [Fact]
    public async Task Get_UnknownCustomer_ReturnsNotFound()
    {
        var tenant = await api.CreateTenantAsync("cust-404");
        using var staff = await OwnerClientAsync(tenant);

        var response = await staff.GetAsync($"/api/customers/{Guid.CreateVersion7()}", Cancel);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_ChangesNameAndEmail()
    {
        var tenant = await api.CreateTenantAsync("cust-upd");
        using var staff = await OwnerClientAsync(tenant);
        var customer = await CreateCustomerAsync(staff, "01712-345678", "Rahim");

        var response = await staff.PutAsJsonAsync($"/api/customers/{customer.Id}", new { name = "Rahim Uddin", email = "Rahim@Shop.test" }, Cancel);
        await response.EnsureSuccessWithBodyAsync();

        var stored = await staff.GetFromJsonAsync<CustomerResponse>($"/api/customers/{customer.Id}", Cancel);
        Assert.Equal(("Rahim Uddin", "rahim@shop.test", "+8801712345678"), (stored!.Name, stored.Email, stored.Phone));
    }

    [Fact]
    public async Task Addresses_AddUpdateRemove_KeepsExactlyOneDefault()
    {
        var tenant = await api.CreateTenantAsync("cust-addr");
        using var staff = await OwnerClientAsync(tenant);
        var customer = await CreateCustomerAsync(staff, "01712-345678", "Rahim Uddin");

        var home = (await AddAddressAsync(staff, customer.Id, "Home", makeDefault: false)).Addresses.Single();
        var withOffice = await AddAddressAsync(staff, customer.Id, "Office", makeDefault: false);
        var office = withOffice.Addresses.Single(a => a.Label == "Office");
        var moved = await PutAddressAsync(staff, customer.Id, office.Id, "Office", makeDefault: true);
        var removed = await staff.DeleteAsync($"/api/customers/{customer.Id}/addresses/{office.Id}", Cancel);
        await removed.EnsureSuccessWithBodyAsync();
        var final = await staff.GetFromJsonAsync<CustomerResponse>($"/api/customers/{customer.Id}", Cancel);

        Assert.True(home.IsDefault);
        Assert.Equal("+8801712345678", home.RecipientPhone);
        Assert.Equal(home.Id, Assert.Single(withOffice.Addresses, a => a.IsDefault).Id);
        Assert.Equal(office.Id, Assert.Single(moved.Addresses, a => a.IsDefault).Id);
        Assert.Equal(office.Id, moved.Addresses[0].Id);
        var remaining = Assert.Single(final!.Addresses);
        Assert.Equal((home.Id, true), (remaining.Id, remaining.IsDefault));
    }

    [Fact]
    public async Task Addresses_UnknownAddress_ReturnsNotFound()
    {
        var tenant = await api.CreateTenantAsync("cust-addr404");
        using var staff = await OwnerClientAsync(tenant);
        var customer = await CreateCustomerAsync(staff, "01712-345678", "Rahim Uddin");

        var response = await staff.DeleteAsync($"/api/customers/{customer.Id}/addresses/{Guid.CreateVersion7()}", Cancel);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Addresses_InvalidRecipientPhone_ReturnsValidationProblem()
    {
        var tenant = await api.CreateTenantAsync("cust-addrbad");
        using var staff = await OwnerClientAsync(tenant);
        var customer = await CreateCustomerAsync(staff, "01712-345678", "Rahim Uddin");

        var response = await staff.PostAsJsonAsync($"/api/customers/{customer.Id}/addresses", Address("Home", makeDefault: false) with { RecipientPhone = "999" }, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Database_RejectsSecondDefaultAddress()
    {
        var tenant = await api.CreateTenantAsync("cust-db");
        using var staff = await OwnerClientAsync(tenant);
        var customer = await CreateCustomerAsync(staff, "01712-345678", "Rahim Uddin");
        await AddAddressAsync(staff, customer.Id, "Home", makeDefault: false);
        await using var scope = await api.CreateTenantScopeAsync(tenant.Identifier);
        var db = scope.ServiceProvider.GetRequiredService<CustomersDbContext>();

        var insert = () => db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO customers."Addresses" ("Id", "CustomerId", "RecipientName", "RecipientPhone", "Line1", "Area", "City", "IsDefault", "CreatedAt")
            VALUES ({Guid.CreateVersion7()}, {customer.Id}, 'Second', '+8801712345678', 'Road 1', 'Mirpur', 'Dhaka', TRUE, now())
            """,
            Cancel);

        var error = await Assert.ThrowsAsync<PostgresException>(insert);
        Assert.Equal(PostgresErrorCodes.ExclusionViolation, error.SqlState);
    }

    [Fact]
    public async Task Customers_WithoutCustomersPermission_AreForbidden()
    {
        var tenant = await api.CreateTenantAsync("cust-perm");
        using var owner = await OwnerClientAsync(tenant);
        (await owner.PostAsJsonAsync("/api/identity/users", new { email = "catalog@shop.test", fullName = "Catalog", password = "Staff-pass-123", roles = new[] { "Catalog Manager" } }, Cancel))
            .EnsureSuccessStatusCode();
        using var catalog = api.CreateTenantClient(tenant.Identifier, await api.SignInAsync(tenant.Identifier, "catalog@shop.test", "Staff-pass-123"));
        using var anonymous = api.CreateTenantClient(tenant.Identifier);

        var byCatalog = await catalog.GetAsync("/api/customers", Cancel);
        var byAnonymous = await anonymous.GetAsync("/api/customers", Cancel);

        Assert.Equal(HttpStatusCode.Forbidden, byCatalog.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, byAnonymous.StatusCode);
    }

    private async Task<CustomerInfo> FindOrCreateAsync(TestTenant tenant, string phone, string? name)
    {
        await using var scope = await api.CreateTenantScopeAsync(tenant.Identifier);
        var directory = scope.ServiceProvider.GetRequiredService<ICustomerDirectory>();
        var result = await directory.FindOrCreateByPhoneAsync(phone, name, Cancel);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        return result.Value;
    }

    private static async Task<CustomerResponse> CreateCustomerAsync(HttpClient staff, string phone, string name, string? email = null) =>
        await SendAsync(staff.PostAsJsonAsync("/api/customers", new { phone, name, email }, Cancel));

    private static async Task<CustomerResponse> AddAddressAsync(HttpClient staff, Guid customerId, string label, bool makeDefault) =>
        await SendAsync(staff.PostAsJsonAsync($"/api/customers/{customerId}/addresses", Address(label, makeDefault), Cancel));

    private static async Task<CustomerResponse> PutAddressAsync(HttpClient staff, Guid customerId, Guid addressId, string label, bool makeDefault) =>
        await SendAsync(staff.PutAsJsonAsync($"/api/customers/{customerId}/addresses/{addressId}", Address(label, makeDefault), Cancel));

    private static AddressBody Address(string label, bool makeDefault) =>
        new(label, "Rahim Uddin", "01712-345678", "House 12, Road 5", null, "Dhanmondi", "Dhaka", "1205", makeDefault);

    private static async Task<CustomerResponse> SendAsync(Task<HttpResponseMessage> request)
    {
        var response = await request;
        await response.EnsureSuccessWithBodyAsync();
        return (await response.Content.ReadFromJsonAsync<CustomerResponse>(Cancel))!;
    }

    private async Task<HttpClient> OwnerClientAsync(TestTenant tenant) =>
        api.CreateTenantClient(tenant.Identifier, await api.SignInAsync(tenant.Identifier, tenant.OwnerEmail, tenant.OwnerPassword));

    private sealed record AddressBody(
        string? Label,
        string RecipientName,
        string RecipientPhone,
        string Line1,
        string? Line2,
        string Area,
        string City,
        string? PostalCode,
        bool MakeDefault);

    private sealed record AddressResponse(Guid Id, string? Label, string RecipientPhone, bool IsDefault);

    private sealed record CustomerResponse(Guid Id, string Phone, string? Name, string? Email, IReadOnlyList<AddressResponse> Addresses);

    private sealed record CustomerItem(Guid Id, string Phone, string? Name, int AddressCount);

    private sealed record CustomerPage(IReadOnlyList<CustomerItem> Items, int TotalCount);
}
