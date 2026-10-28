using ClarioShop.Ordering.Domain;

namespace ClarioShop.Ordering.Tests.Domain;

public sealed class OrderPaymentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, "Unpaid")]
    [InlineData(0.01, "PartiallyPaid")]
    [InlineData(899.99, "PartiallyPaid")]
    [InlineData(900, "Paid")]
    public void PaymentStatusFor_AmountPaid_ReturnsStatus(decimal amountPaid, string expected) =>
        Assert.Equal(Enum.Parse<OrderPaymentStatus>(expected), Order.PaymentStatusFor(900m, amountPaid));

    [Fact]
    public void RecordPayment_TwoParts_AddsUpToPaid()
    {
        var order = Place();

        order.RecordPayment(Guid.CreateVersion7(), "CashOnDelivery", 500m, Now, Now.AddMinutes(1));
        var afterFirst = order.PaymentStatus;
        order.RecordPayment(Guid.CreateVersion7(), "CashOnDelivery", 400m, Now, Now.AddMinutes(2));

        Assert.Equal(OrderPaymentStatus.PartiallyPaid, afterFirst);
        Assert.Equal((900m, OrderPaymentStatus.Paid, Now.AddMinutes(2), 2), (order.AmountPaid, order.PaymentStatus, order.UpdatedAt, order.Payments.Count));
    }

    [Fact]
    public void RecordPayment_SamePaymentTwice_CountsItOnce()
    {
        var order = Place();
        var paymentId = Guid.CreateVersion7();
        order.RecordPayment(paymentId, "CashOnDelivery", 500m, Now, Now.AddMinutes(1));

        var recordedAgain = order.RecordPayment(paymentId, "CashOnDelivery", 500m, Now, Now.AddMinutes(5));

        Assert.False(recordedAgain);
        Assert.Equal((500m, Now.AddMinutes(1)), (order.AmountPaid, order.UpdatedAt));
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
            delivery: null,
            vatRatePercent: 0m,
            Guid.CreateVersion7(),
            Now);
}
