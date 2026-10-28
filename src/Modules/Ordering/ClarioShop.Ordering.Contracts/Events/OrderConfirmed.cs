using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Ordering.Contracts.Events;

/// <summary>
/// Staff (or, later, a payment) confirmed the order: its stock is sold. <paramref name="ReservationId"/> holds that stock
/// and may differ from the one in <see cref="OrderPlaced"/> when the first hold expired and the stock was reserved again.
/// </summary>
public sealed record OrderConfirmed(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    Guid ReservationId) : IIntegrationEvent;
