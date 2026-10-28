using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClarioShop.Infrastructure.Messaging;
using ClarioShop.Infrastructure.Tenancy;
using ClarioShop.Ordering.Contracts.Events;
using ClarioShop.SharedKernel.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace ClarioShop.IntegrationTests.Features;

/// <summary>
/// Reporting builds its own copy of each order from Ordering's events and reports on it: sales per day (confirmed
/// orders, net of cancellations and returns), orders by status and the best-selling variants.
/// </summary>
public sealed partial class OrderingTests
{
    [Fact]
    public async Task DailySales_CountsConfirmedOrdersNetOfCancellationsAndReturns()
    {
        var shop = await ShopAsync("rep-daily");
        await PlaceOrdersInEveryStatusAsync(shop);

        var report = await shop.Staff.GetFromJsonAsync<DailySalesReport>($"/api/reports/sales/daily?{AroundToday()}", Cancel);

        // Confirmed (900), packed (450) and delivered (899.50); not the placed, cancelled or returned ones.
        Assert.Equal((3, 2249.5m, 0m, 2249.5m), (report!.Totals.Orders, report.Totals.Subtotal, report.Totals.DeliveryFees, report.Totals.Total));
        Assert.Equal(3, report.Days.Count);
        Assert.Equal((3, 2249.5m), (report.Days.Sum(day => day.Sales.Orders), report.Days.Sum(day => day.Sales.Total)));
    }

    [Fact]
    public async Task OrdersByStatus_CountsEachOrderInItsCurrentStatus()
    {
        var shop = await ShopAsync("rep-status");
        await PlaceOrdersInEveryStatusAsync(shop);

        var report = await shop.Staff.GetFromJsonAsync<StatusReport>($"/api/reports/orders/by-status?{AroundToday()}", Cancel);

        Assert.Equal(
            [("PendingPayment", 0), ("Placed", 1), ("Confirmed", 1), ("Packed", 1), ("Shipped", 0), ("Delivered", 1), ("Cancelled", 1), ("Returned", 1)],
            report!.Statuses.Select(status => (status.Status, status.Orders)));
        Assert.Equal((6, 899.5m), (report.TotalOrders, report.Statuses.Single(status => status.Status == "Delivered").Total));
    }

    [Fact]
    public async Task TopVariants_RanksByRevenueOrByQuantity()
    {
        var shop = await ShopAsync("rep-top");
        var rice = await shop.StockedVariantAsync("Miniket Rice", "RICE-5KG", price: 450m, stock: 10);
        var soap = await shop.StockedVariantAsync("Lux Soap", "SOAP-LUX", price: 45m, stock: 20);
        var oil = await shop.StockedVariantAsync("Rupchanda Oil", "OIL-5L", price: 899.5m, stock: 10);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        foreach (var (variant, quantity) in new[] { (rice, 2m), (rice, 1m), (soap, 10m), (oil, 1m) })
        {
            var order = await PlaceOrderAsync(shopper, variant, quantity);
            await ChangeStatusAsync(shop, order.Id, "confirm");
        }

        await WaitForReportAsync(shop, report => report.Statuses.Single(status => status.Status == "Confirmed").Orders == 4);

        var byRevenue = await shop.Staff.GetFromJsonAsync<TopVariantsReport>($"/api/reports/sales/top-variants?{AroundToday()}", Cancel);
        var byQuantity = await shop.Staff.GetFromJsonAsync<TopVariantsReport>($"/api/reports/sales/top-variants?{AroundToday()}&sortBy=quantity&limit=2", Cancel);

        Assert.Equal(
            [("RICE-5KG", "Miniket Rice", 3m, 1350m, 2), ("OIL-5L", "Rupchanda Oil", 1m, 899.5m, 1), ("SOAP-LUX", "Lux Soap", 10m, 450m, 1)],
            byRevenue!.Items.Select(item => (item.Sku, item.ProductName, item.Quantity, item.Revenue, item.Orders)));
        Assert.Equal("quantity", byQuantity!.SortBy);
        Assert.Equal([soap, rice], byQuantity.Items.Select(item => item.VariantId));
    }

    [Fact]
    public async Task Reports_EventsDeliveredAgainAfterLaterOnes_CountTheOrderOnceInItsLatestStatus()
    {
        var shop = await ShopAsync("rep-again");
        var rice = await shop.StockedVariantAsync("Miniket Rice", "RICE-5KG", price: 450m, stock: 10);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        var order = await PlaceOrderAsync(shopper, rice, quantity: 2);
        foreach (var action in new[] { "confirm", "pack", "ship", "deliver" })
        {
            await ChangeStatusAsync(shop, order.Id, action);
        }

        await WaitForReportAsync(shop, report => report.Statuses.Single(status => status.Status == "Delivered").Orders == 1);
        var placed = await api.Events.WaitForAsync<OrderPlaced>(e => e.OrderId == order.Id, EventTimeout);
        var confirmed = await api.Events.WaitForAsync<OrderConfirmed>(e => e.OrderId == order.Id, EventTimeout);

        // Delivered again under new message ids, so the inbox does not stop them: the projection must.
        await RedeliverAsync(placed);
        await RedeliverAsync(confirmed);

        var statuses = await shop.Staff.GetFromJsonAsync<StatusReport>($"/api/reports/orders/by-status?{AroundToday()}", Cancel);
        var sales = await shop.Staff.GetFromJsonAsync<DailySalesReport>($"/api/reports/sales/daily?{AroundToday()}", Cancel);
        var top = await shop.Staff.GetFromJsonAsync<TopVariantsReport>($"/api/reports/sales/top-variants?{AroundToday()}", Cancel);
        Assert.Equal((1, 1), (statuses!.TotalOrders, statuses.Statuses.Single(status => status.Status == "Delivered").Orders));
        Assert.Equal((1, 900m), (sales!.Totals.Orders, sales.Totals.Total));
        Assert.Equal(2m, Assert.Single(top!.Items).Quantity);
    }

    [Fact]
    public async Task Reports_EventsOfOneOrderHandledAtOnce_KeepEveryChange()
    {
        var shop = await ShopAsync("rep-race");
        var tenantId = await TenantIdAsync(shop);
        var now = DateTimeOffset.UtcNow;

        // Each order's events race to create Reporting's copy of it, then to change it.
        await Task.WhenAll(Enumerable.Range(1, 10).Select(async number =>
        {
            var orderId = Guid.CreateVersion7();
            var orderNumber = $"CS-{number:000000}";
            var line = new OrderPlacedLine(Guid.CreateVersion7(), "RICE-5KG", 1m, 450m, 450m, "Miniket Rice", "RICE-5KG");
            IIntegrationEvent[] events =
            [
                new OrderPlaced(Guid.CreateVersion7(), now, orderId, orderNumber, Guid.CreateVersion7(), "CashOnDelivery", 450m, 0m, 0m, 450m, Guid.CreateVersion7(), [line]),
                new OrderConfirmed(Guid.CreateVersion7(), now.AddSeconds(1), orderId, orderNumber, Guid.CreateVersion7(), Guid.CreateVersion7()),
                new OrderPacked(Guid.CreateVersion7(), now.AddSeconds(2), orderId, orderNumber, Guid.CreateVersion7()),
            ];
            await Task.WhenAll(events.Select(integrationEvent => Task.Run(() => DispatchAsync(tenantId, integrationEvent), Cancel)));
        }));

        var statuses = await shop.Staff.GetFromJsonAsync<StatusReport>($"/api/reports/orders/by-status?{AroundToday()}", Cancel);
        var sales = await shop.Staff.GetFromJsonAsync<DailySalesReport>($"/api/reports/sales/daily?{AroundToday()}", Cancel);
        Assert.Equal((10, 10), (statuses!.TotalOrders, statuses.Statuses.Single(status => status.Status == "Packed").Orders));
        Assert.Equal((10, 4500m), (sales!.Totals.Orders, sales.Totals.Total));
    }

    [Fact]
    public async Task Reports_NeedReportsSalesViewAndAValidPeriod()
    {
        var shop = await ShopAsync("rep-auth");
        using var handler = await StaffWithRoleAsync(shop, $"orders@{shop.Tenant.Identifier}.test", "Order Handler");
        using var anonymous = api.CreateTenantClient(shop.Tenant.Identifier);
        string[] reports = ["/api/reports/sales/daily", "/api/reports/sales/top-variants", "/api/reports/orders/by-status"];

        foreach (var url in reports)
        {
            var asAnonymous = await anonymous.GetAsync(url, Cancel);
            var asHandler = await handler.GetAsync(url, Cancel);
            var asOwner = await shop.Staff.GetAsync(url, Cancel);
            var backwards = await shop.Staff.GetAsync($"{url}?from=2026-10-10&to=2026-10-01", Cancel);
            var tooLong = await shop.Staff.GetAsync($"{url}?from=2025-01-01&to=2026-01-02", Cancel);

            Assert.Equal(
                (HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.BadRequest),
                (asAnonymous.StatusCode, asHandler.StatusCode, asOwner.StatusCode, backwards.StatusCode, tooLong.StatusCode));
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await shop.Staff.GetAsync("/api/reports/sales/top-variants?limit=0", Cancel)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.Staff.GetAsync("/api/reports/sales/top-variants?sortBy=price", Cancel)).StatusCode);
    }

    [Fact]
    public async Task DailySales_NoPeriodGiven_ListsTheLast30DaysWithZeros()
    {
        var shop = await ShopAsync("rep-default");

        var report = await shop.Staff.GetFromJsonAsync<DailySalesReport>("/api/reports/sales/daily", Cancel);

        Assert.Equal(30, report!.Days.Count);
        Assert.Equal((report.From, report.To), (report.Days[0].Date, report.Days[^1].Date));
        Assert.All(report.Days, day => Assert.Equal(0, day.Sales.Orders));
    }

    /// <summary>
    /// Six orders, all placed today: one left placed, one confirmed, one packed, one delivered, one cancelled after it
    /// was confirmed and one returned after it shipped. Waits until Reporting has seen every change.
    /// </summary>
    private async Task PlaceOrdersInEveryStatusAsync(Shop shop)
    {
        var rice = await shop.StockedVariantAsync("Miniket Rice", "RICE-5KG", price: 450m, stock: 20);
        var oil = await shop.StockedVariantAsync("Rupchanda Oil", "OIL-5L", price: 899.5m, stock: 5);
        using var shopper = api.CreateTenantClient(shop.Tenant.Identifier);
        string[][] steps =
        [
            [],
            ["confirm"],
            ["confirm", "pack"],
            ["confirm", "pack", "ship", "deliver"],
            ["confirm", "cancel"],
            ["confirm", "pack", "ship", "return"],
        ];
        var quantities = new[] { 1m, 2m, 1m, 1m, 3m, 4m };

        for (var index = 0; index < steps.Length; index++)
        {
            var order = await PlaceOrderAsync(shopper, index == 3 ? oil : rice, quantities[index]);
            foreach (var action in steps[index])
            {
                await ChangeStatusAsync(shop, order.Id, action);
            }
        }

        await WaitForReportAsync(shop, report =>
            report.TotalOrders == 6 && report.Statuses.Count(status => status.Orders == 1) == 6);
    }

    private async Task<Guid> TenantIdAsync(Shop shop)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var tenant = await scope.ServiceProvider.GetRequiredService<ITenantStore>().FindByIdentifierAsync(shop.Tenant.Identifier, Cancel);
        return tenant!.Id;
    }

    /// <summary>Hands the event to Reporting's handler alone, as its queue would.</summary>
    private Task DispatchAsync(Guid tenantId, IIntegrationEvent integrationEvent)
    {
        var message = new OutgoingMessage(
            Guid.CreateVersion7(),
            IntegrationEventNames.For(integrationEvent.GetType()),
            JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), JsonSerializerOptions.Web),
            tenantId);
        return api.Services.GetRequiredService<IntegrationEventDispatcher>().DispatchAsync(message, queueName: "clarioshop.reporting", Cancel);
    }

    /// <summary>From yesterday to tomorrow in UTC, which holds today in the store's time zone too.</summary>
    private static string AroundToday()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return $"from={today.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}&to={today.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
    }

    /// <summary>Waits for Reporting to catch up with the orders' events.</summary>
    private static async Task WaitForReportAsync(Shop shop, Func<StatusReport, bool> condition)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        timeout.CancelAfter(EventTimeout);
        while (true)
        {
            var report = await shop.Staff.GetFromJsonAsync<StatusReport>($"/api/reports/orders/by-status?{AroundToday()}", Cancel);
            if (condition(report!))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), timeout.Token);
        }
    }

    private sealed record SalesFigures(int Orders, decimal Subtotal, decimal DeliveryFees, decimal Vat, decimal Total);

    private sealed record SalesDay(DateOnly Date, SalesFigures Sales);

    private sealed record DailySalesReport(DateOnly From, DateOnly To, IReadOnlyList<SalesDay> Days, SalesFigures Totals);

    private sealed record StatusCount(string Status, int Orders, decimal Total);

    private sealed record StatusReport(IReadOnlyList<StatusCount> Statuses, int TotalOrders);

    private sealed record VariantSales(Guid VariantId, string Sku, string ProductName, string VariantName, decimal Quantity, decimal Revenue, int Orders);

    private sealed record TopVariantsReport(string SortBy, IReadOnlyList<VariantSales> Items);
}
