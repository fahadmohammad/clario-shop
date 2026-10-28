using ClarioShop.Reporting.Domain;

namespace ClarioShop.Reporting.Tests.Domain;

public sealed class SalesOrderTests
{
    private static readonly DateTimeOffset PlacedAt = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly Today = new(2026, 10, 28);

    [Fact]
    public void RecordPlaced_CopiesAmountsAndLinesInOrder()
    {
        var order = Placed();

        Assert.Equal((SalesOrderStatus.Placed, 950m, 60m, 1010m, (DateOnly?)Today), (order.Status, order.Subtotal, order.DeliveryFee, order.Total, order.PlacedOn));
        Assert.Equal([(1, "RICE-5KG", 900m), (2, "SOAP-LUX", 50m)], order.Lines.Select(line => (line.Position, line.Sku, line.LineTotal)));
    }

    [Fact]
    public void RecordPlaced_SecondTime_ChangesNothing()
    {
        var order = Placed();

        order.RecordPlaced(Placement(SalesOrderStatus.Placed, PlacedAt.AddMinutes(1), subtotal: 1m));

        Assert.Equal((2, 950m, PlacedAt), (order.Lines.Count, order.Subtotal, order.StatusChangedAt));
    }

    [Fact]
    public void RecordPlaced_AfterALaterStatus_KeepsThatStatus()
    {
        var order = SalesOrder.Start(Guid.CreateVersion7(), "CS-000001", SalesOrderStatus.Shipped, PlacedAt.AddHours(2));

        order.RecordPlaced(Placement(SalesOrderStatus.Placed, PlacedAt));

        Assert.Equal((SalesOrderStatus.Shipped, 950m, 2), (order.Status, order.Subtotal, order.Lines.Count));
    }

    [Fact]
    public void MoveTo_OlderChange_IsIgnored()
    {
        var order = Placed();
        order.MoveTo(SalesOrderStatus.Delivered, PlacedAt.AddDays(1));

        var moved = order.MoveTo(SalesOrderStatus.Shipped, PlacedAt.AddHours(5));

        Assert.False(moved);
        Assert.Equal(SalesOrderStatus.Delivered, order.Status);
    }

    [Fact]
    public void MoveTo_SameInstant_TheLaterStageWins()
    {
        var order = Placed();
        var at = PlacedAt.AddHours(1);

        order.MoveTo(SalesOrderStatus.Packed, at);
        order.MoveTo(SalesOrderStatus.Confirmed, at);

        Assert.Equal(SalesOrderStatus.Packed, order.Status);
    }

    [Fact]
    public void RecordConfirmed_MakesItASaleOnThatDay()
    {
        var order = Placed();

        order.RecordConfirmed(PlacedAt.AddHours(1), Today.AddDays(1));

        Assert.Equal((SalesOrderStatus.Confirmed, (DateOnly?)Today.AddDays(1), true), (order.Status, order.SoldOn, order.IsSale));
    }

    [Fact]
    public void RecordConfirmed_AgainLater_KeepsTheFirstSaleDay()
    {
        var order = Placed();
        order.RecordConfirmed(PlacedAt.AddHours(1), Today);

        order.RecordConfirmed(PlacedAt.AddDays(3), Today.AddDays(3));

        Assert.Equal((DateOnly?)Today, order.SoldOn);
    }

    [Theory]
    [InlineData("Cancelled", false)]
    [InlineData("Returned", false)]
    [InlineData("Delivered", true)]
    public void IsSale_ConfirmedThenMoved_CountsUnlessCancelledOrReturned(string status, bool isSale)
    {
        var order = Placed();
        order.RecordConfirmed(PlacedAt.AddHours(1), Today);

        order.MoveTo(Enum.Parse<SalesOrderStatus>(status), PlacedAt.AddHours(2));

        Assert.Equal(isSale, order.IsSale);
    }

    [Fact]
    public void IsSale_NeverConfirmed_IsFalse()
    {
        var order = Placed();

        order.MoveTo(SalesOrderStatus.Cancelled, PlacedAt.AddHours(1));

        Assert.Equal(((DateOnly?)null, false), (order.SoldOn, order.IsSale));
    }

    private static SalesOrder Placed()
    {
        var order = SalesOrder.Start(Guid.CreateVersion7(), "CS-000001", SalesOrderStatus.Placed, PlacedAt);
        order.RecordPlaced(Placement(SalesOrderStatus.Placed, PlacedAt));
        return order;
    }

    private static SalesOrderPlacement Placement(SalesOrderStatus status, DateTimeOffset at, decimal subtotal = 950m) => new(
        status,
        "CashOnDelivery",
        subtotal,
        DeliveryFee: 60m,
        Vat: 0m,
        Total: subtotal + 60m,
        at,
        DateOnly.FromDateTime(at.UtcDateTime),
        [
            new(Guid.CreateVersion7(), "RICE-5KG", "Miniket Rice", "5kg", 2m, 450m, 900m),
            new(Guid.CreateVersion7(), "SOAP-LUX", "Lux Soap", "100g", 1m, 50m, 50m),
        ]);
}
