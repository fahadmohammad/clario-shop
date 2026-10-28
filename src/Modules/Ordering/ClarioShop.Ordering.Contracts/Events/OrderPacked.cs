using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Ordering.Contracts.Events;

/// <summary>Staff packed the order; it waits for the courier or rider.</summary>
public sealed record OrderPacked(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId) : IIntegrationEvent;
