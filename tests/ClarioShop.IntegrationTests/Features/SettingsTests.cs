using System.Net;
using System.Net.Http.Json;
using ClarioShop.IntegrationTests.Infrastructure;
using ClarioShop.Settings.Contracts;
using ClarioShop.Settings.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClarioShop.IntegrationTests.Features;

[Collection(ApiCollection.Name)]
public sealed class SettingsTests(ClarioApiFactory api)
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Storefront_NewTenant_ReturnsDefaultsNamedAfterTenantToAnonymousCallers()
    {
        var tenant = await api.CreateTenantAsync("set-def");
        using var anonymous = api.CreateTenantClient(tenant.Identifier);

        var storefront = await anonymous.GetFromJsonAsync<StorefrontResponse>("/api/settings/storefront", Cancel);

        Assert.Equal($"Shop {tenant.Identifier}", storefront!.StoreName);
        Assert.Equal(("BDT", "৳", CurrencySymbolPosition.Before, DigitGrouping.SouthAsian), (storefront.Currency.Code, storefront.Currency.Symbol, storefront.Currency.SymbolPosition, storefront.Currency.Grouping));
        Assert.Equal("en", storefront.DefaultLanguage);
        Assert.Equal(["en", "bn"], storefront.Languages);
        Assert.Matches("^#[0-9A-F]{6}$", storefront.Theme.PrimaryColor);
        Assert.Empty(storefront.DeliveryZones);
    }

    [Fact]
    public async Task UpdateStore_AfterQueriesCachedOldValues_QueriesReturnNewValues()
    {
        var tenant = await api.CreateTenantAsync("set-store");
        using var owner = await OwnerClientAsync(tenant);
        var before = await QueryStoreSettingsAsync(tenant);

        var response = await owner.PutAsJsonAsync("/api/settings/store", ValidStore(), Cancel);
        await response.EnsureSuccessWithBodyAsync();
        var after = await QueryStoreSettingsAsync(tenant);
        var stored = await owner.GetFromJsonAsync<StoreSettingsInfo>("/api/settings", Cancel);

        Assert.Equal((0m, "INV"), (before.VatRatePercent, before.InvoicePrefix));
        Assert.Equal((15m, "CM", "Clario Mart", "bn"), (after.VatRatePercent, after.InvoicePrefix, after.Profile.StoreName, after.DefaultLanguage));
        Assert.Equal(["bn", "en"], after.Languages);
        Assert.Equal(("info@clario.test", "Dhanmondi", CurrencySymbolPosition.After), (stored!.Profile.ContactEmail, stored.Profile.Area, stored.Currency.SymbolPosition));
    }

    [Fact]
    public async Task UpdateTheme_AfterStorefrontWasRead_StorefrontShowsNewTheme()
    {
        var tenant = await api.CreateTenantAsync("set-theme");
        using var owner = await OwnerClientAsync(tenant);
        using var anonymous = api.CreateTenantClient(tenant.Identifier);
        await anonymous.GetFromJsonAsync<StorefrontResponse>("/api/settings/storefront", Cancel);

        var response = await owner.PutAsJsonAsync("/api/settings/theme", ValidTheme(), Cancel);
        await response.EnsureSuccessWithBodyAsync();
        var storefront = await anonymous.GetFromJsonAsync<StorefrontResponse>("/api/settings/storefront", Cancel);

        Assert.Equal(("#FF5722", 0.75m, "Noto Sans Bengali", "https://cdn.clario.test/logo.png"), (storefront!.Theme.PrimaryColor, storefront.Theme.RadiusRem, storefront.Theme.HeadingFont, storefront.Theme.LogoUrl));
    }

    [Fact]
    public async Task UpdateStore_ConcurrentFirstSaves_KeepOneSettingsRow()
    {
        var tenant = await api.CreateTenantAsync("set-race");
        var token = await api.SignInAsync(tenant.Identifier, tenant.OwnerEmail, tenant.OwnerPassword);

        var saves = Enumerable.Range(0, 6).Select(async i =>
        {
            using var owner = api.CreateTenantClient(tenant.Identifier, token);
            return await owner.PutAsJsonAsync("/api/settings/store", ValidStore() with { InvoicePrefix = $"P{i}" }, Cancel);
        });
        var responses = await Task.WhenAll(saves);

        Assert.All(responses, response => Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);
        await using var scope = await api.CreateTenantScopeAsync(tenant.Identifier);
        var db = scope.ServiceProvider.GetRequiredService<SettingsDbContext>();
        Assert.Equal(1, await db.StoreSettings.CountAsync(Cancel));
    }

    [Theory]
    [InlineData("""{ "vatRatePercent": 100.5 }""")]
    [InlineData("""{ "invoicePrefix": "CS 01" }""")]
    [InlineData("""{ "languages": ["en", "fr"] }""")]
    [InlineData("""{ "defaultLanguage": "bn", "languages": ["en"] }""")]
    [InlineData("""{ "profile": { "storeName": "", "contactEmail": null } }""")]
    [InlineData("""{ "currency": { "symbol": "৳", "symbolPosition": "Before", "decimalDigits": 3, "grouping": "Thousands" } }""")]
    public async Task UpdateStore_InvalidInput_ReturnsValidationProblem(string overrides)
    {
        var tenant = await api.CreateTenantAsync("set-inv");
        using var owner = await OwnerClientAsync(tenant);

        var response = await owner.PutAsync("/api/settings/store", Merge(ValidStore(), overrides), Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("""{ "primaryColor": "red" }""")]
    [InlineData("""{ "accentColor": "#12345" }""")]
    [InlineData("""{ "bodyFont": "Arial; color: red" }""")]
    [InlineData("""{ "logoUrl": "http://cdn.clario.test/logo.png" }""")]
    [InlineData("""{ "radiusRem": 5 }""")]
    public async Task UpdateTheme_InvalidInput_ReturnsValidationProblem(string overrides)
    {
        var tenant = await api.CreateTenantAsync("set-thinv");
        using var owner = await OwnerClientAsync(tenant);

        var response = await owner.PutAsync("/api/settings/theme", Merge(ValidTheme(), overrides), Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeliveryZones_CreateUpdateDelete_StorefrontAndQueriesOfferOnlyActiveZonesInOrder()
    {
        var tenant = await api.CreateTenantAsync("set-zones");
        using var owner = await OwnerClientAsync(tenant);
        using var anonymous = api.CreateTenantClient(tenant.Identifier);
        var outside = await CreateZoneAsync(owner, new ZoneBody("Outside Dhaka", ["Gazipur", "Savar"], 120m, null, true, 2));
        var inside = await CreateZoneAsync(owner, new ZoneBody("Inside Dhaka", ["Dhanmondi", "Mirpur"], 60m, 2000m, true, 1));
        var paused = await CreateZoneAsync(owner, new ZoneBody("Chattogram", ["Agrabad"], 150m, null, true, 3));
        await anonymous.GetFromJsonAsync<StorefrontResponse>("/api/settings/storefront", Cancel);

        var updated = await owner.PutAsJsonAsync($"/api/settings/delivery-zones/{paused.Id}", new ZoneBody("Chattogram", ["Agrabad"], 150m, null, false, 3), Cancel);
        await updated.EnsureSuccessWithBodyAsync();
        var raised = await owner.PutAsJsonAsync($"/api/settings/delivery-zones/{outside.Id}", new ZoneBody("Outside Dhaka", ["Gazipur", "Savar"], 130m, 5000m, true, 2), Cancel);
        await raised.EnsureSuccessWithBodyAsync();
        var storefront = await anonymous.GetFromJsonAsync<StorefrontResponse>("/api/settings/storefront", Cancel);
        var all = await owner.GetFromJsonAsync<List<ZoneResponse>>("/api/settings/delivery-zones", Cancel);
        await using var scope = await api.CreateTenantScopeAsync(tenant.Identifier);
        var queries = scope.ServiceProvider.GetRequiredService<ISettingsQueries>();
        var insideInfo = await queries.GetDeliveryZoneAsync(inside.Id, Cancel);
        var pausedInfo = await queries.GetDeliveryZoneAsync(paused.Id, Cancel);
        var deleted = await owner.DeleteAsync($"/api/settings/delivery-zones/{inside.Id}", Cancel);
        var afterDelete = await queries.GetDeliveryZonesAsync(Cancel);

        Assert.Equal(["Inside Dhaka", "Outside Dhaka"], storefront!.DeliveryZones.Select(z => z.Name));
        Assert.Equal((130m, 5000m), (storefront.DeliveryZones[1].Fee, storefront.DeliveryZones[1].FreeDeliveryThreshold));
        Assert.Equal([(inside.Id, true), (outside.Id, true), (paused.Id, false)], all!.Select(z => (z.Id, z.IsActive)));
        Assert.Equal(("Inside Dhaka", 60m, 2000m), (insideInfo!.Name, insideInfo.Fee, insideInfo.FreeDeliveryThreshold));
        Assert.Equal(["Dhanmondi", "Mirpur"], insideInfo.Areas);
        Assert.Null(pausedInfo);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal([outside.Id], afterDelete.Select(z => z.Id));
    }

    [Fact]
    public async Task DeliveryZones_NameTakenIgnoringCase_ReturnsConflict()
    {
        var tenant = await api.CreateTenantAsync("set-zdup");
        using var owner = await OwnerClientAsync(tenant);
        await CreateZoneAsync(owner, new ZoneBody("Inside Dhaka", ["Mirpur"], 60m, null, true, 1));
        var other = await CreateZoneAsync(owner, new ZoneBody("Outside Dhaka", ["Savar"], 120m, null, true, 2));

        var created = await owner.PostAsJsonAsync("/api/settings/delivery-zones", new ZoneBody(" inside dhaka ", ["Gulshan"], 80m, null, true, 3), Cancel);
        var renamed = await owner.PutAsJsonAsync($"/api/settings/delivery-zones/{other.Id}", new ZoneBody("INSIDE DHAKA", ["Savar"], 120m, null, true, 2), Cancel);

        Assert.Equal(HttpStatusCode.Conflict, created.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, renamed.StatusCode);
    }

    [Fact]
    public async Task DeliveryZones_UnknownZone_ReturnsNotFound()
    {
        var tenant = await api.CreateTenantAsync("set-z404");
        using var owner = await OwnerClientAsync(tenant);

        var updated = await owner.PutAsJsonAsync($"/api/settings/delivery-zones/{Guid.CreateVersion7()}", new ZoneBody("Nowhere", [], 10m, null, true, 0), Cancel);
        var deleted = await owner.DeleteAsync($"/api/settings/delivery-zones/{Guid.CreateVersion7()}", Cancel);

        Assert.Equal(HttpStatusCode.NotFound, updated.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);
    }

    [Theory]
    [InlineData("""{ "name": "", "areas": [], "fee": 60 }""")]
    [InlineData("""{ "name": "Inside Dhaka", "areas": [], "fee": -1 }""")]
    [InlineData("""{ "name": "Inside Dhaka", "areas": [], "fee": 60, "freeDeliveryThreshold": 0 }""")]
    [InlineData("""{ "name": "Inside Dhaka", "areas": [], "fee": 60.555 }""")]
    public async Task DeliveryZones_InvalidInput_ReturnsValidationProblem(string body)
    {
        var tenant = await api.CreateTenantAsync("set-zinv");
        using var owner = await OwnerClientAsync(tenant);

        var response = await owner.PostAsync("/api/settings/delivery-zones", new StringContent(body, System.Text.Encoding.UTF8, "application/json"), Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Settings_WithoutSettingsPermission_AreForbidden()
    {
        var tenant = await api.CreateTenantAsync("set-perm");
        using var owner = await OwnerClientAsync(tenant);
        (await owner.PostAsJsonAsync("/api/identity/users", new { email = "orders@shop.test", fullName = "Orders", password = "Staff-pass-123", roles = new[] { "Order Handler" } }, Cancel))
            .EnsureSuccessStatusCode();
        using var staff = api.CreateTenantClient(tenant.Identifier, await api.SignInAsync(tenant.Identifier, "orders@shop.test", "Staff-pass-123"));
        using var anonymous = api.CreateTenantClient(tenant.Identifier);

        var byStaff = await staff.PutAsJsonAsync("/api/settings/theme", ValidTheme(), Cancel);
        var zonesByStaff = await staff.GetAsync("/api/settings/delivery-zones", Cancel);
        var byAnonymous = await anonymous.GetAsync("/api/settings", Cancel);

        Assert.Equal(HttpStatusCode.Forbidden, byStaff.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, zonesByStaff.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, byAnonymous.StatusCode);
    }

    private static StoreBody ValidStore() => new(
        new ProfileBody("Clario Mart", "01712-345678", "Info@Clario.test", "House 12, Road 5", null, "Dhanmondi", "Dhaka", "1205"),
        "cm",
        15m,
        new CurrencyBody("৳", "After", 0, "SouthAsian"),
        "bn",
        ["en", "bn"]);

    private static ThemeBody ValidTheme() =>
        new("#ff5722", "#FFFFFF", "#0EA5E9", "#FFFFFF", "#111827", 0.75m, "Noto Sans Bengali", "Hind Siliguri", "https://cdn.clario.test/logo.png", null);

    /// <summary>The body as JSON with the top-level properties in <paramref name="overrides"/> replaced.</summary>
    private static StringContent Merge<T>(T body, string overrides)
    {
        var json = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(body, System.Text.Json.JsonSerializerOptions.Web))!.AsObject();
        foreach (var (name, value) in System.Text.Json.Nodes.JsonNode.Parse(overrides)!.AsObject())
        {
            json[name] = value?.DeepClone();
        }

        return new StringContent(json.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
    }

    private async Task<StoreSettingsInfo> QueryStoreSettingsAsync(TestTenant tenant)
    {
        await using var scope = await api.CreateTenantScopeAsync(tenant.Identifier);
        return await scope.ServiceProvider.GetRequiredService<ISettingsQueries>().GetStoreSettingsAsync(Cancel);
    }

    private static async Task<ZoneResponse> CreateZoneAsync(HttpClient owner, ZoneBody zone)
    {
        var response = await owner.PostAsJsonAsync("/api/settings/delivery-zones", zone, Cancel);
        await response.EnsureSuccessWithBodyAsync();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ZoneResponse>(Cancel))!;
    }

    private async Task<HttpClient> OwnerClientAsync(TestTenant tenant) =>
        api.CreateTenantClient(tenant.Identifier, await api.SignInAsync(tenant.Identifier, tenant.OwnerEmail, tenant.OwnerPassword));

    private sealed record ProfileBody(string StoreName, string? ContactPhone, string? ContactEmail, string? AddressLine1, string? AddressLine2, string? Area, string? City, string? PostalCode);

    private sealed record CurrencyBody(string Symbol, string SymbolPosition, int DecimalDigits, string Grouping);

    private sealed record StoreBody(ProfileBody Profile, string InvoicePrefix, decimal VatRatePercent, CurrencyBody Currency, string DefaultLanguage, IReadOnlyList<string> Languages);

    private sealed record ThemeBody(
        string PrimaryColor,
        string PrimaryForegroundColor,
        string AccentColor,
        string BackgroundColor,
        string ForegroundColor,
        decimal RadiusRem,
        string HeadingFont,
        string BodyFont,
        string? LogoUrl,
        string? FaviconUrl);

    private sealed record ZoneBody(string Name, IReadOnlyList<string> Areas, decimal Fee, decimal? FreeDeliveryThreshold, bool IsActive, int SortOrder);

    private sealed record ZoneResponse(Guid Id, string Name, IReadOnlyList<string> Areas, decimal Fee, decimal? FreeDeliveryThreshold, bool IsActive, int SortOrder);

    private sealed record StorefrontResponse(
        string StoreName,
        string? ContactPhone,
        string? ContactEmail,
        ThemeInfo Theme,
        CurrencyFormatInfo Currency,
        string DefaultLanguage,
        IReadOnlyList<string> Languages,
        IReadOnlyList<DeliveryZoneInfo> DeliveryZones);
}
