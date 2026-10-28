using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Ordering.Contracts.Events;

/// <summary>The shopper refused the parcel or sent the delivered order back.</summary>
/// <param name="PreviousStatus"><c>Shipped</c> or <c>Delivered</c>.</param>
/// <param name="Reason">The note staff gave, if any.</param>
public sealed record OrderReturned(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    string PreviousStatus,
    string? Reason) : IIntegrationEvent;
