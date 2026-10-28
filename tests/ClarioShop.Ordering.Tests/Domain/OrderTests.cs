using ClarioShop.Ordering.Domain;

namespace ClarioShop.Ordering.Tests.Domain;

public sealed class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PlaceCashOnDelivery_StartsPlacedWithOneHistoryEntry()
    {
        var order = Place(Line("RICE-5KG", 450m, 2));

        Assert.Equal(OrderStatus.Placed, order.Status);
        Assert.Equal(PaymentMethod.CashOnDelivery, order.PaymentMethod);
        var change = Assert.Single(order.History);
        Assert.Equal((null, OrderStatus.Placed, Now), (change.FromStatus, change.ToStatus, change.ChangedAt));
    }

    [Fact]
    public void PlaceCashOnDelivery_TotalsAreSumOfLineTotals()
    {
        var order = Place(Line("RICE-5KG", 450m, 2), Line("OIL-5L", 899.50m, 1));

        Assert.Equal(1799.50m, order.Subtotal);
        Assert.Equal(order.Subtotal, order.Total);
    }

    [Fact]
    public void PlaceCashOnDelivery_LinesKeepCartOrder()
    {
        var order = Place(Line("A", 10m, 1), Line("B", 20m, 1), Line("C", 30m, 1));

        Assert.Equal(["A", "B", "C"], order.Lines.OrderBy(line => line.Position).Select(line => line.Sku));
        Assert.Equal([1, 2, 3], order.Lines.Select(line => line.Position).Order());
    }

    [Theory]
    [InlineData(380.00, 1.5, 570.00)]
    [InlineData(123.45, 0.333, 41.11)] // 41.10885
    [InlineData(10.10, 0.125, 1.26)] // 1.2625
    [InlineData(0.50, 0.005, 0.00)] // 0.0025
    [InlineData(1.00, 0.005, 0.01)] // 0.005, half away from zero
    public void LineTotal_WeighedItem_RoundsToPoisha(decimal unitPrice, decimal quantity, decimal expected)
    {
        var order = Place(Line("FISH-RUI", unitPrice, quantity));

        Assert.Equal(expected, Assert.Single(order.Lines).LineTotal);
    }

    [Fact]
    public void PlaceCashOnDelivery_TrimsBlankNoteToNull()
    {
        var order = Order.PlaceCashOnDelivery(
            Guid.CreateVersion7(), 1, Guid.CreateVersion7(), Customer(), Address(), "   ", [Line("A", 1m, 1)], Guid.CreateVersion7(), Now);

        Assert.Null(order.Note);
    }

    [Theory]
    [InlineData(1, "CS-000001")]
    [InlineData(123, "CS-000123")]
    [InlineData(1234567, "CS-1234567")]
    public void OrderNumber_Format_PadsToSixDigits(long number, string expected) =>
        Assert.Equal(expected, OrderNumber.Format(number));

    private static Order Place(params OrderLineSpec[] lines) =>
        Order.PlaceCashOnDelivery(
            Guid.CreateVersion7(), 42, Guid.CreateVersion7(), Customer(), Address(), note: null, lines, Guid.CreateVersion7(), Now);

    private static OrderLineSpec Line(string sku, decimal price, decimal quantity) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), $"Product {sku}", sku, sku, price, quantity);

    private static OrderCustomer Customer() => new(Guid.CreateVersion7(), "+8801712345678", "Rahim Uddin");

    private static DeliveryAddress Address() => new("House 12, Road 5", null, "Dhanmondi", "Dhaka", "1205");
}
