using System.Net;
using System.Net.Http.Json;
using ClarioShop.IntegrationTests.Infrastructure;

namespace ClarioShop.IntegrationTests.Features;

/// <summary>
/// A store can write its own text for each notification (Settings), checked on save, and Notifications sends it in
/// place of the built-in text until the store resets it.
/// </summary>
public sealed partial class OrderingTests
{
    private const string TemplatesUrl = "/api/settings/notification-templates";

    [Fact]
    public async Task ListTemplates_NewStore_ListsEveryMessageWithBuiltInText()
    {
        var shop = await ShopAsync("tpl-list");

        var templates = await shop.Staff.GetFromJsonAsync<IReadOnlyList<TemplateRow>>(TemplatesUrl, Cancel);

        Assert.Equal(16, templates!.Count);
        Assert.All(templates, template => Assert.False(template.IsCustomized));
        var placedSms = Assert.Single(templates, t => (t.Event, t.Channel, t.Language) == ("OrderPlaced", "Sms", "en"));
        Assert.Equal(["StoreName", "CustomerName", "OrderNumber", "Total"], placedSms.Placeholders);
        Assert.Equal(placedSms.DefaultBody, placedSms.Body);
        var shippedEmail = Assert.Single(templates, t => (t.Event, t.Channel, t.Language) == ("OrderShipped", "Email", "bn"));
        Assert.Equal(["StoreName", "CustomerName", "OrderNumber"], shippedEmail.Placeholders);
        Assert.NotNull(shippedEmail.Subject);
    }

    [Fact]
    public async Task UpdateTemplate_AfterTemplatesWereCached_NextSmsUsesStoreText()
    {
        var (shop, storeName) = await NotifyingShopAsync("tpl-own", "en");
        var variant = await shop.StockedVariantAsync("Miniket Rice 5kg", "RICE-5KG", price: 450m, stock: 10);
        var phone = NewPhone();
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlaceOrderForAsync(shopper, variant, quantity: 1, phone);
        await WaitForNotificationsAsync(shop, order.Id, rows => rows.Count == 1 && rows[0].Status == "Sent");

        var saved = await shop.Staff.PutAsJsonAsync(
            $"{TemplatesUrl}/orderconfirmed/sms/EN",
            new { body = "{StoreName}: {CustomerName}, order {OrderNumber} is confirmed. Call 09612-000000 for help." },
            Cancel);
        await saved.EnsureSuccessWithBodyAsync();
        await ChangeStatusAsync(shop, order.Id, "confirm");
        await WaitForNotificationsAsync(shop, order.Id, rows => rows.Count == 2 && rows.All(row => row.Status == "Sent"));

        var template = await saved.Content.ReadFromJsonAsync<TemplateRow>(Cancel);
        Assert.Equal(("OrderConfirmed", "Sms", "en", true), (template!.Event, template.Channel, template.Language, template.IsCustomized));
        Assert.Equal(
            $"{storeName}: Rahim Uddin, order {order.Number} is confirmed. Call 09612-000000 for help.",
            Assert.Single(api.Sms.MessagesTo(E164(phone), "is confirmed")));
    }

    [Fact]
    public async Task ResetTemplate_AfterStoreText_SendsBuiltInTextAgain()
    {
        var (shop, storeName) = await NotifyingShopAsync("tpl-reset", "bn");
        var variant = await shop.StockedVariantAsync("Ispahani Tea", "TEA-400G", price: 250m, stock: 5);
        var url = $"{TemplatesUrl}/OrderPlaced/Sms/bn";
        (await shop.Staff.PutAsJsonAsync(url, new { body = "{StoreName}: অর্ডার {OrderNumber}, মোট {Total}" }, Cancel)).EnsureSuccessStatusCode();
        var phone = NewPhone();
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var first = await PlaceOrderForAsync(shopper, variant, quantity: 1, phone);
        await WaitForNotificationsAsync(shop, first.Id, rows => rows.Count == 1 && rows[0].Status == "Sent");

        var reset = await shop.Staff.DeleteAsync(url, Cancel);
        await reset.EnsureSuccessWithBodyAsync();
        var second = await PlaceOrderForAsync(shopper, variant, quantity: 1, phone);
        await WaitForNotificationsAsync(shop, second.Id, rows => rows.Count == 1 && rows[0].Status == "Sent");
        var resetTwice = await shop.Staff.DeleteAsync(url, Cancel);

        Assert.Equal(
            [
                $"{storeName}: অর্ডার {first.Number}, মোট ৳250",
                $"{storeName}: ধন্যবাদ! আপনার অর্ডার {second.Number} পেয়েছি, মোট ৳250। নিশ্চিত করতে আমরা আপনাকে কল করব।",
            ],
            api.Sms.MessagesTo(E164(phone), storeName));
        Assert.False((await reset.Content.ReadFromJsonAsync<TemplateRow>(Cancel))!.IsCustomized);
        Assert.Equal(HttpStatusCode.OK, resetTwice.StatusCode);
    }

    [Fact]
    public async Task UpdateTemplate_EmailWithStoreText_SendsStoreSubjectAndBody()
    {
        var (shop, storeName) = await NotifyingShopAsync("tpl-email", "en");
        var variant = await shop.StockedVariantAsync("Lux Soap", "SOAP-LUX", price: 45m, stock: 10);
        var phone = NewPhone();
        var email = $"karim.{Guid.NewGuid():N}@example.com";
        (await shop.Staff.PostAsJsonAsync("/api/customers", new { phone, name = "Karim", email }, Cancel)).EnsureSuccessStatusCode();
        (await shop.Staff.PutAsJsonAsync(
            $"{TemplatesUrl}/OrderPlaced/Email/en",
            new { subject = "Order {OrderNumber} at {StoreName}", body = "Hi {CustomerName}, you pay {Total} on delivery." },
            Cancel)).EnsureSuccessStatusCode();
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);

        var order = await PlaceOrderForAsync(shopper, variant, quantity: 2, phone);

        await WaitForNotificationsAsync(shop, order.Id, rows => rows.Count == 2 && rows.All(row => row.Status == "Sent"));
        var sent = Assert.Single(api.Email.To(email));
        Assert.Equal(($"Order {order.Number} at {storeName}", "Hi Karim, you pay ৳90 on delivery."), (sent.Subject, sent.Body));
    }

    [Theory]
    [InlineData("OrderShipped/Sms/en", "{StoreName}: {OrderNumber} is on its way, total {Total}", null, "body")]
    [InlineData("OrderShipped/Sms/en", "{StoreName}: {ordernumber} is on its way", null, "body")]
    [InlineData("OrderShipped/Sms/en", "{StoreName}: {OrderNumber is on its way", null, "body")]
    [InlineData("OrderShipped/Sms/en", "{StoreName}: {OrderNumber} is on its way", "Shipped", "subject")]
    [InlineData("OrderShipped/Email/en", "{StoreName}: {OrderNumber} is on its way", null, "subject")]
    [InlineData("OrderShipped/Email/en", "{StoreName}: {OrderNumber} is on its way", "Order {Order}", "subject")]
    [InlineData("OrderShipped/Sms/en", "", null, "Body")]
    public async Task UpdateTemplate_InvalidText_Returns400AndKeepsBuiltInText(string message, string body, string? subject, string field)
    {
        var shop = await ShopAsync("tpl-bad");

        var response = await shop.Staff.PutAsJsonAsync($"{TemplatesUrl}/{message}", new { subject, body }, Cancel);
        var templates = await shop.Staff.GetFromJsonAsync<IReadOnlyList<TemplateRow>>(TemplatesUrl, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>(Cancel);
        Assert.Contains(field, problem!.Errors.Keys);
        Assert.All(templates!, template => Assert.False(template.IsCustomized));
    }

    [Fact]
    public async Task UpdateTemplate_SmsLongerThanSevenParts_Returns400()
    {
        var shop = await ShopAsync("tpl-long");

        var response = await shop.Staff.PutAsJsonAsync($"{TemplatesUrl}/OrderShipped/Sms/en", new { body = "{OrderNumber} " + new string('x', 470) }, Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("OrderReturned/Sms/en")]
    [InlineData("OrderShipped/Push/en")]
    [InlineData("OrderShipped/Sms/fr")]
    public async Task UpdateTemplate_UnknownMessage_Returns404(string message)
    {
        var shop = await ShopAsync("tpl-404");

        var update = await shop.Staff.PutAsJsonAsync($"{TemplatesUrl}/{message}", new { body = "{OrderNumber}" }, Cancel);
        var reset = await shop.Staff.DeleteAsync($"{TemplatesUrl}/{message}", Cancel);

        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound), (update.StatusCode, reset.StatusCode));
    }

    [Fact]
    public async Task Templates_NeedSettingsEdit()
    {
        var shop = await ShopAsync("tpl-auth");
        using var handler = await StaffWithRoleAsync(shop, $"orders@{shop.Tenant.Identifier}.test", "Order Handler");
        using var anonymous = api.CreateTenantClient(shop.Tenant.Identifier);
        var url = $"{TemplatesUrl}/OrderShipped/Sms/en";

        var statuses = new[]
        {
            (await anonymous.GetAsync(TemplatesUrl, Cancel)).StatusCode,
            (await handler.GetAsync(TemplatesUrl, Cancel)).StatusCode,
            (await handler.PutAsJsonAsync(url, new { body = "{OrderNumber}" }, Cancel)).StatusCode,
            (await handler.DeleteAsync(url, Cancel)).StatusCode,
        };

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden, HttpStatusCode.Forbidden], statuses);
    }

    [Fact]
    public async Task UpdateTemplate_ConcurrentFirstSaves_KeepOneTemplate()
    {
        var shop = await ShopAsync("tpl-race");

        var saves = Enumerable.Range(0, 6).Select(i =>
            shop.Staff.PutAsJsonAsync($"{TemplatesUrl}/OrderShipped/Sms/en", new { body = $"{{OrderNumber}} shipped ({i})" }, Cancel));
        var responses = await Task.WhenAll(saves);
        var templates = await shop.Staff.GetFromJsonAsync<IReadOnlyList<TemplateRow>>(TemplatesUrl, Cancel);

        Assert.All(responses, response => Assert.Contains(response.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(templates!, template => template.IsCustomized);
    }

    private sealed record TemplateRow(
        string Event,
        string Channel,
        string Language,
        string? Subject,
        string Body,
        bool IsCustomized,
        IReadOnlyList<string> Placeholders,
        string? DefaultSubject,
        string DefaultBody,
        DateTimeOffset? UpdatedAt);

    private sealed record ValidationProblem(Dictionary<string, string[]> Errors);
}
