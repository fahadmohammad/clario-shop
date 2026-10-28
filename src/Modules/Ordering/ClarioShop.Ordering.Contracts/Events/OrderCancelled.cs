using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Ordering.Contracts.Events;

/// <summary>The order was cancelled before it shipped; the stock held or sold for it goes back.</summary>
/// <param name="PreviousStatus">E.g. <c>Placed</c> or <c>Packed</c>.</param>
/// <param name="Reason">The note staff gave, if any.</param>
public sealed record OrderCancelled(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    Guid ReservationId,
    string PreviousStatus,
    string? Reason) : IIntegrationEvent;
