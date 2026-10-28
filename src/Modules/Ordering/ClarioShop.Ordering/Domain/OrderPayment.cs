using ClarioShop.SharedKernel.Domain;

namespace ClarioShop.Ordering.Domain;

/// <summary>A payment Payments reported for the order; its id is the Payments module's payment id, so each counts once.</summary>
internal sealed class OrderPayment : Entity
{
    private OrderPayment()
    {
    }

    public Guid OrderId { get; private set; }

    /// <summary>E.g. <c>CashOnDelivery</c>, as Payments names it.</summary>
    public string Method { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public DateTimeOffset PaidAt { get; private set; }

    internal static OrderPayment Create(Guid paymentId, Guid orderId, string method, decimal amount, DateTimeOffset paidAt) => new()
    {
        Id = paymentId,
        OrderId = orderId,
        Method = method,
        Amount = amount,
        PaidAt = paidAt,
    };
}
