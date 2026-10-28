using ClarioShop.SharedKernel.Domain;

namespace ClarioShop.Ordering.Domain;

/// <summary>One row of an order's status history.</summary>
internal sealed class OrderStatusChange : Entity
{
    private OrderStatusChange()
    {
    }

    public Guid OrderId { get; private set; }

    /// <summary>Null for the status the order was created in.</summary>
    public OrderStatus? FromStatus { get; private set; }

    public OrderStatus ToStatus { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    /// <summary>The staff user who made the change; null when the shopper or the system did.</summary>
    public Guid? ChangedByUserId { get; private set; }

    public string? Note { get; private set; }

    internal static OrderStatusChange Create(Guid orderId, OrderStatus? from, OrderStatus to, DateTimeOffset now, Guid? userId, string? note) => new()
    {
        Id = Guid.CreateVersion7(),
        OrderId = orderId,
        FromStatus = from,
        ToStatus = to,
        ChangedAt = now,
        ChangedByUserId = userId,
        Note = note,
    };
}
