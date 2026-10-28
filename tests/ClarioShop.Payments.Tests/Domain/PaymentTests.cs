using ClarioShop.Payments.Domain;

namespace ClarioShop.Payments.Tests.Domain;

public sealed class PaymentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CashCollected_ValidAmount_SucceedsWithOneAttempt()
    {
        var orderId = Guid.CreateVersion7();
        var rider = Guid.CreateVersion7();

        var payment = Payment.CashCollected(orderId, "CS-000001", 1250.50m, rider, "  Paid in notes  ", Now);

        Assert.Equal((orderId, PaymentMethod.CashOnDelivery, PaymentStatus.Succeeded, 1250.50m), (payment.OrderId, payment.Method, payment.Status, payment.Amount));
        Assert.Equal((Now, (DateTimeOffset?)Now), (payment.CreatedAt, payment.CompletedAt));
        var attempt = Assert.Single(payment.Attempts);
        Assert.Equal((payment.Id, AttemptOutcome.Succeeded, rider, "Paid in notes"), (attempt.PaymentId, attempt.Outcome, attempt.RecordedByUserId, attempt.Note));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void CashCollected_ZeroOrNegativeAmount_Throws(decimal amount) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Payment.CashCollected(Guid.CreateVersion7(), "CS-000001", amount, null, null, Now));
}
