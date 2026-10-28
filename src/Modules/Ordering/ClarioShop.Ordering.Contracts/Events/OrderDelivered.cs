using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Ordering.Contracts.Events;

/// <summary>The shopper received the order.</summary>
public sealed record OrderDelivered(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId) : IIntegrationEvent;
