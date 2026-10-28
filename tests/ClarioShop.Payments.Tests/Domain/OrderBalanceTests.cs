using ClarioShop.Payments.Domain;

namespace ClarioShop.Payments.Tests.Domain;

public sealed class OrderBalanceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid OrderId = Guid.CreateVersion7();

    [Theory]
    [InlineData(0, "Unpaid", 1000)]
    [InlineData(400, "PartiallyPaid", 600)]
    [InlineData(1000, "Paid", 0)]
    public void Of_SucceededPayments_SumsPaidAndOutstanding(decimal paid, string status, decimal outstanding)
    {
        var payments = paid == 0m ? [] : new[] { Cash(paid) };

        var balance = OrderBalance.Of(1000m, payments);

        Assert.Equal((paid, Enum.Parse<OrderPaymentStatus>(status), outstanding), (balance.Paid, balance.Status, balance.Outstanding));
    }

    [Fact]
    public void Of_SeveralPayments_AddsThemUp()
    {
        var balance = OrderBalance.Of(1799.50m, [Cash(1000m), Cash(799.50m)]);

        Assert.Equal((1799.50m, 0m, OrderPaymentStatus.Paid), (balance.Paid, balance.Outstanding, balance.Status));
    }

    [Theory]
    [InlineData(0, 1000)]
    [InlineData(0, 0.01)]
    [InlineData(400, 600)]
    public void CanAccept_UpToOutstanding_Succeeds(decimal paid, decimal amount) =>
        Assert.True(new OrderBalance(1000m, paid).CanAccept(amount).IsSuccess);

    [Theory]
    [InlineData(0, 1000.01)]
    [InlineData(400, 600.01)]
    [InlineData(1000, 1)]
    public void CanAccept_MoreThanOutstanding_ReturnsConflict(decimal paid, decimal amount)
    {
        var result = new OrderBalance(1000m, paid).CanAccept(amount);

        Assert.Equal("payments.exceeds_outstanding", result.Error.Code);
    }

    private static Payment Cash(decimal amount) => Payment.CashCollected(OrderId, "CS-000001", amount, null, null, Now);
}
