using ClarioShop.Ordering.Domain;
using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Ordering.Tests.Domain;

public sealed class OrderLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Staff = Guid.CreateVersion7();

    [Theory]
    [InlineData("Placed", "Confirmed")]
    [InlineData("Placed", "Cancelled")]
    [InlineData("Confirmed", "Packed")]
    [InlineData("Confirmed", "Cancelled")]
    [InlineData("Packed", "Shipped")]
    [InlineData("Packed", "Cancelled")]
    [InlineData("Shipped", "Delivered")]
    [InlineData("Shipped", "Returned")]
    [InlineData("Delivered", "Returned")]
    [InlineData("PendingPayment", "Cancelled")]
    public void IsAllowed_LifecycleStep_ReturnsTrue(string from, string to) =>
        Assert.True(OrderTransitions.IsAllowed(Enum.Parse<OrderStatus>(from), Enum.Parse<OrderStatus>(to)));

    [Theory]
    [InlineData("Placed", "Packed")]
    [InlineData("Placed", "Shipped")]
    [InlineData("Placed", "Returned")]
    [InlineData("Confirmed", "Placed")]
    [InlineData("Confirmed", "Confirmed")]
    [InlineData("Shipped", "Cancelled")]
    [InlineData("Delivered", "Cancelled")]
    [InlineData("Cancelled", "Confirmed")]
    [InlineData("Returned", "Delivered")]
    [InlineData("PendingPayment", "Confirmed")]
    public void IsAllowed_SkipOrBackwardOrFromFinal_ReturnsFalse(string from, string to) =>
        Assert.False(OrderTransitions.IsAllowed(Enum.Parse<OrderStatus>(from), Enum.Parse<OrderStatus>(to)));

    [Fact]
    public void IsAllowed_EveryStatus_HasARule()
    {
        foreach (var status in Enum.GetValues<OrderStatus>())
        {
            _ = OrderTransitions.IsAllowed(status, OrderStatus.Cancelled);
        }
    }

    [Fact]
    public void Confirm_Placed_RecordsHistoryAndNewReservation()
    {
        var order = Place();
        var reservation = Guid.CreateVersion7();

        var result = order.Confirm(reservation, Staff, "  Called the customer  ", Now.AddMinutes(20));

        Assert.True(result.IsSuccess);
        Assert.Equal((OrderStatus.Confirmed, reservation, Now.AddMinutes(20)), (order.Status, order.ReservationId, order.UpdatedAt));
        var change = order.History.Single(c => c.ToStatus == OrderStatus.Confirmed);
        Assert.Equal((OrderStatus.Placed, Staff, "Called the customer"), (change.FromStatus, change.ChangedByUserId, change.Note));
    }

    [Fact]
    public void Confirm_AlreadyConfirmed_ReturnsConflictAndKeepsReservation()
    {
        var order = Place();
        var first = Guid.CreateVersion7();
        order.Confirm(first, Staff, note: null, Now);

        var result = order.Confirm(Guid.CreateVersion7(), Staff, note: null, Now);

        Assert.Equal(("ordering.invalid_transition", ErrorType.Conflict), (result.Error.Code, result.Error.Type));
        Assert.Equal(first, order.ReservationId);
        Assert.Equal(2, order.History.Count);
    }

    [Fact]
    public void MoveTo_FullLifecycle_WritesEveryStepToHistory()
    {
        var order = Place();

        order.Confirm(order.ReservationId, Staff, note: null, Now);
        order.MoveTo(OrderStatus.Packed, Staff, note: null, Now);
        order.MoveTo(OrderStatus.Shipped, Staff, note: null, Now);
        order.MoveTo(OrderStatus.Delivered, Staff, note: null, Now);

        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal(
            [OrderStatus.Placed, OrderStatus.Confirmed, OrderStatus.Packed, OrderStatus.Shipped, OrderStatus.Delivered],
            order.History.Select(change => change.ToStatus));
    }

    [Fact]
    public void MoveTo_CancelAfterShipping_ReturnsConflictAndChangesNothing()
    {
        var order = Place();
        order.Confirm(order.ReservationId, Staff, note: null, Now);
        order.MoveTo(OrderStatus.Packed, Staff, note: null, Now);
        order.MoveTo(OrderStatus.Shipped, Staff, note: null, Now);

        var result = order.MoveTo(OrderStatus.Cancelled, Staff, "Too late", Now);

        Assert.Equal("ordering.invalid_transition", result.Error.Code);
        Assert.Equal(OrderStatus.Shipped, order.Status);
        Assert.Equal(4, order.History.Count);
    }

    private static Order Place() =>
        Order.PlaceCashOnDelivery(
            Guid.CreateVersion7(),
            7,
            Guid.CreateVersion7(),
            new OrderCustomer(Guid.CreateVersion7(), "+8801712345678", "Rahim Uddin"),
            new DeliveryAddress("House 12, Road 5", null, "Dhanmondi", "Dhaka", "1205"),
            note: null,
            [new OrderLineSpec(Guid.CreateVersion7(), Guid.CreateVersion7(), "Rice", "5kg", "RICE-5KG", 450m, 2)],
            Guid.CreateVersion7(),
            Now);
}
