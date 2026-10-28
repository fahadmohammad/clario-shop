using ClarioShop.Ordering.Domain;

namespace ClarioShop.Ordering.Tests.Domain;

public sealed class PrepaidOrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Place_SslCommerz_StartsPendingPayment()
    {
        var order = Place(PaymentMethod.SslCommerz);

        Assert.Equal((OrderStatus.PendingPayment, PaymentMethod.SslCommerz), (order.Status, order.PaymentMethod));
        var created = Assert.Single(order.History);
        Assert.Equal(((OrderStatus?)null, OrderStatus.PendingPayment), (created.FromStatus, created.ToStatus));
    }

    [Fact]
    public void Place_CashOnDelivery_StartsPlaced() =>
        Assert.Equal(OrderStatus.Placed, Place(PaymentMethod.CashOnDelivery).Status);

    [Fact]
    public void ConfirmPaid_PaidInFull_ConfirmsOnTheCurrentReservation()
    {
        var order = Place(PaymentMethod.SslCommerz);
        order.RecordPayment(Guid.CreateVersion7(), "SslCommerz", 900m, Now, Now);
        var reservationId = Guid.CreateVersion7();

        var confirmed = order.ConfirmPaid(reservationId, Now.AddMinutes(1));

        Assert.True(confirmed.IsSuccess);
        Assert.Equal((OrderStatus.Confirmed, reservationId), (order.Status, order.ReservationId));
        var change = order.History.Last();
        Assert.Equal(((OrderStatus?)OrderStatus.PendingPayment, OrderStatus.Confirmed, (Guid?)null, "Paid online"), (change.FromStatus, change.ToStatus, change.ChangedByUserId, change.Note));
    }

    [Fact]
    public void ConfirmPaid_PartlyPaid_ReturnsAwaitingPayment()
    {
        var order = Place(PaymentMethod.SslCommerz);
        order.RecordPayment(Guid.CreateVersion7(), "SslCommerz", 899.99m, Now, Now);

        var confirmed = order.ConfirmPaid(Guid.CreateVersion7(), Now);

        Assert.Equal("ordering.awaiting_payment", confirmed.Error.Code);
        Assert.Equal(OrderStatus.PendingPayment, order.Status);
    }

    [Fact]
    public void ConfirmPaid_CashOnDeliveryOrder_ReturnsInvalidTransition()
    {
        var order = Place(PaymentMethod.CashOnDelivery);
        order.RecordPayment(Guid.CreateVersion7(), "CashOnDelivery", 900m, Now, Now);

        var confirmed = order.ConfirmPaid(Guid.CreateVersion7(), Now);

        Assert.Equal("ordering.invalid_transition", confirmed.Error.Code);
    }

    [Fact]
    public void PendingPayment_StaffCannotConfirm() =>
        Assert.False(Place(PaymentMethod.SslCommerz).CanMoveTo(OrderStatus.Confirmed));

    [Fact]
    public void CancelUnpaid_PendingPayment_CancelsWithTheReason()
    {
        var order = Place(PaymentMethod.SslCommerz);

        var cancelled = order.CancelUnpaid("No payment confirmed.", Now.AddHours(1));

        Assert.True(cancelled.IsSuccess);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        var change = order.History.Last();
        Assert.Equal(((OrderStatus?)OrderStatus.PendingPayment, (Guid?)null, "Payment failed: No payment confirmed."), (change.FromStatus, change.ChangedByUserId, change.Note));
    }

    [Fact]
    public void CancelUnpaid_PartlyPaid_ReturnsPartlyPaid()
    {
        var order = Place(PaymentMethod.SslCommerz);
        order.RecordPayment(Guid.CreateVersion7(), "SslCommerz", 100m, Now, Now);

        var cancelled = order.CancelUnpaid("No payment confirmed.", Now);

        Assert.Equal("ordering.partly_paid", cancelled.Error.Code);
        Assert.Equal(OrderStatus.PendingPayment, order.Status);
    }

    [Fact]
    public void CancelUnpaid_AlreadyCancelled_ReturnsInvalidTransition()
    {
        var order = Place(PaymentMethod.SslCommerz);
        order.CancelUnpaid("First failure.", Now);

        var cancelled = order.CancelUnpaid("No payment confirmed.", Now);

        Assert.Equal("ordering.invalid_transition", cancelled.Error.Code);
        Assert.Equal(2, order.History.Count);
    }

    [Fact]
    public void CancelUnpaid_CashOnDeliveryOrder_ReturnsInvalidTransition() =>
        Assert.Equal("ordering.invalid_transition", Place(PaymentMethod.CashOnDelivery).CancelUnpaid("No payment confirmed.", Now).Error.Code);

    private static Order Place(PaymentMethod method) =>
        Order.Place(
            Guid.CreateVersion7(),
            7,
            Guid.CreateVersion7(),
            new OrderCustomer(Guid.CreateVersion7(), "+8801712345678", "Rahim Uddin"),
            new DeliveryAddress("House 12, Road 5", null, "Dhanmondi", "Dhaka", "1205"),
            note: null,
            [new OrderLineSpec(Guid.CreateVersion7(), Guid.CreateVersion7(), "Rice", "5kg", "RICE-5KG", 450m, 2)],
            delivery: null,
            vatRatePercent: 0m,
            method,
            Guid.CreateVersion7(),
            Now);
}
