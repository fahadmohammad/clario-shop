using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClarioShop.IntegrationTests.Infrastructure;

namespace ClarioShop.IntegrationTests.Features;

/// <summary>Changing a staff member's roles, the last-Owner guard, and the Identity audit log.</summary>
[Collection(ApiCollection.Name)]
public sealed class StaffRolesTests(ClarioApiFactory api)
{
    private const string StaffPassword = "Staff-pass-123";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ChangeUserRoles_ReplacesRolesAndPermissionsFollowOnNextSignIn()
    {
        var tenant = await api.CreateTenantAsync("roles");
        using var owner = await OwnerClientAsync(tenant);
        var clerkId = await CreateUserAsync(owner, "clerk@shop.test", "Inventory Clerk");

        var changed = await PutRolesAsync(owner, clerkId, "Order Handler");
        using var staff = api.CreateTenantClient(tenant.Identifier, await api.SignInAsync(tenant.Identifier, "clerk@shop.test", StaffPassword));
        var me = await staff.GetFromJsonAsync<MeResponse>("/api/identity/me", Cancel);

        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(["Order Handler"], (await changed.Content.ReadFromJsonAsync<RolesResponse>(Cancel))!.Roles);
        Assert.Contains("orders.manage", me!.Permissions);
        Assert.DoesNotContain("inventory.adjust", me.Permissions);
    }

    [Fact]
    public async Task ChangeUserRoles_WritesAuditRowWithRolesBeforeAndAfter()
    {
        var tenant = await api.CreateTenantAsync("roles-audit");
        using var owner = await OwnerClientAsync(tenant);
        owner.DefaultRequestHeaders.Add("X-Device-Id", "back-office-pc");
        var clerkId = await CreateUserAsync(owner, "clerk@shop.test", "Inventory Clerk");

        (await PutRolesAsync(owner, clerkId, "Inventory Clerk", "Catalog Manager")).EnsureSuccessStatusCode();
        var log = await owner.GetFromJsonAsync<AuditPage>($"/api/identity/audit-log?entityId={clerkId}&action=identity.user.roles_changed", Cancel);

        var entry = Assert.Single(log!.Items);
        Assert.Equal(("User", clerkId, "back-office-pc"), (entry.EntityType, entry.EntityId, entry.Device));
        Assert.NotNull(entry.UserId);
        Assert.Equal(["Inventory Clerk"], Roles(entry.Before!.Value));
        Assert.Equal(["Catalog Manager", "Inventory Clerk"], Roles(entry.After!.Value));
    }

    [Fact]
    public async Task ChangeUserRoles_SameRoles_WritesNoAuditRow()
    {
        var tenant = await api.CreateTenantAsync("roles-same");
        using var owner = await OwnerClientAsync(tenant);
        var clerkId = await CreateUserAsync(owner, "clerk@shop.test", "Inventory Clerk");

        var response = await PutRolesAsync(owner, clerkId, "inventory clerk");
        var log = await owner.GetFromJsonAsync<AuditPage>($"/api/identity/audit-log?entityId={clerkId}&action=identity.user.roles_changed", Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, log!.TotalCount);
    }

    [Fact]
    public async Task CreateUser_WritesAuditRowWithRoles()
    {
        var tenant = await api.CreateTenantAsync("roles-create");
        using var owner = await OwnerClientAsync(tenant);

        var clerkId = await CreateUserAsync(owner, "clerk@shop.test", "Inventory Clerk");
        var log = await owner.GetFromJsonAsync<AuditPage>($"/api/identity/audit-log?entityId={clerkId}", Cancel);

        var entry = Assert.Single(log!.Items);
        Assert.Equal("identity.user.created", entry.Action);
        Assert.Null(entry.Before);
        Assert.Equal(["Inventory Clerk"], Roles(entry.After!.Value));
        Assert.Equal("clerk@shop.test", entry.After.Value.GetProperty("email").GetString());
    }

    [Fact]
    public async Task ChangeUserRoles_LastActiveOwner_IsRefused()
    {
        var tenant = await api.CreateTenantAsync("roles-last");
        using var owner = await OwnerClientAsync(tenant);
        var me = await owner.GetFromJsonAsync<MeResponse>("/api/identity/me", Cancel);

        var response = await PutRolesAsync(owner, me!.Id, "Inventory Clerk");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancel);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("only active Owner", problem.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChangeUserRoles_OwnerWithAnotherOwner_CanStepDown()
    {
        var tenant = await api.CreateTenantAsync("roles-step");
        using var owner = await OwnerClientAsync(tenant);
        var me = await owner.GetFromJsonAsync<MeResponse>("/api/identity/me", Cancel);
        await CreateUserAsync(owner, "partner@shop.test", "Owner");

        var response = await PutRolesAsync(owner, me!.Id, "Catalog Manager");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ChangeUserRoles_TwoOwnersDemoteEachOtherAtOnce_OneOwnerRemains()
    {
        const int Rounds = 5;
        for (var round = 0; round < Rounds; round++)
        {
            var tenant = await api.CreateTenantAsync("roles-race");
            using var owner = await OwnerClientAsync(tenant);
            var firstId = (await owner.GetFromJsonAsync<MeResponse>("/api/identity/me", Cancel))!.Id;
            var secondId = await CreateUserAsync(owner, "partner@shop.test", "Owner");
            using var partner = api.CreateTenantClient(tenant.Identifier, await api.SignInAsync(tenant.Identifier, "partner@shop.test", StaffPassword));

            var results = await Task.WhenAll(PutRolesAsync(owner, secondId, "Catalog Manager"), PutRolesAsync(partner, firstId, "Catalog Manager"));

            Assert.Equal(
                [HttpStatusCode.OK, HttpStatusCode.Conflict],
                results.Select(r => r.StatusCode).Order());
        }
    }

    [Theory]
    [InlineData("Night Shift", HttpStatusCode.BadRequest)]
    [InlineData("", HttpStatusCode.BadRequest)]
    public async Task ChangeUserRoles_InvalidRoles_IsRefused(string role, HttpStatusCode expected)
    {
        var tenant = await api.CreateTenantAsync("roles-bad");
        using var owner = await OwnerClientAsync(tenant);
        var clerkId = await CreateUserAsync(owner, "clerk@shop.test", "Inventory Clerk");

        var response = await PutRolesAsync(owner, clerkId, role);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task ChangeUserRoles_UnknownUser_ReturnsNotFound()
    {
        var tenant = await api.CreateTenantAsync("roles-404");
        using var owner = await OwnerClientAsync(tenant);

        var response = await PutRolesAsync(owner, Guid.CreateVersion7(), "Inventory Clerk");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChangeUserRolesAndAuditLog_NeedTheirPermissions()
    {
        var tenant = await api.CreateTenantAsync("roles-auth");
        using var owner = await OwnerClientAsync(tenant);
        var clerkId = await CreateUserAsync(owner, "clerk@shop.test", "Inventory Clerk");
        using var clerk = api.CreateTenantClient(tenant.Identifier, await api.SignInAsync(tenant.Identifier, "clerk@shop.test", StaffPassword));
        using var anonymous = api.CreateTenantClient(tenant.Identifier);

        var clerkChangesOwnRoles = await PutRolesAsync(clerk, clerkId, "Owner");
        var anonymousChanges = await PutRolesAsync(anonymous, clerkId, "Owner");
        var clerkReadsLog = await clerk.GetAsync("/api/identity/audit-log", Cancel);

        Assert.Equal(
            (HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden),
            (clerkChangesOwnRoles.StatusCode, anonymousChanges.StatusCode, clerkReadsLog.StatusCode));
    }

    [Fact]
    public async Task NewTenant_AuditsTheOwnerItWasCreatedWith()
    {
        var tenant = await api.CreateTenantAsync("roles-tenant");
        using var owner = await OwnerClientAsync(tenant);

        var log = await owner.GetFromJsonAsync<AuditPage>("/api/identity/audit-log?action=identity.user.created", Cancel);

        var entry = Assert.Single(log!.Items);
        Assert.Equal(["Owner"], Roles(entry.After!.Value));
    }

    private static List<string> Roles(JsonElement values) =>
        values.GetProperty("roles").EnumerateArray().Select(role => role.GetString()!).ToList();

    private static Task<HttpResponseMessage> PutRolesAsync(HttpClient client, Guid userId, params string[] roles) =>
        client.PutAsJsonAsync($"/api/identity/users/{userId}/roles", new { roles }, Cancel);

    private static async Task<Guid> CreateUserAsync(HttpClient owner, string email, string role)
    {
        var response = await owner.PostAsJsonAsync("/api/identity/users", new { email, fullName = role, password = StaffPassword, roles = new[] { role } }, Cancel);
        await response.EnsureSuccessWithBodyAsync();
        return (await response.Content.ReadFromJsonAsync<CreatedUser>(Cancel))!.Id;
    }

    private async Task<HttpClient> OwnerClientAsync(TestTenant tenant) =>
        api.CreateTenantClient(tenant.Identifier, await api.SignInAsync(tenant.Identifier, tenant.OwnerEmail, tenant.OwnerPassword));

    private sealed record CreatedUser(Guid Id);

    private sealed record RolesResponse(Guid Id, IReadOnlyList<string> Roles);

    private sealed record MeResponse(Guid Id, string Email, IReadOnlyList<string> Permissions);

    private sealed record AuditItem(string Action, string EntityType, Guid EntityId, JsonElement? Before, JsonElement? After, Guid? UserId, string? Device);

    private sealed record AuditPage(IReadOnlyList<AuditItem> Items, int TotalCount);
}
