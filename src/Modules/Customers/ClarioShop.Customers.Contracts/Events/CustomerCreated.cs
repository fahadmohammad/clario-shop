using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Customers.Contracts.Events;

/// <param name="Phone">E.164, e.g. <c>+8801712345678</c>.</param>
public sealed record CustomerCreated(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid CustomerId,
    string Phone,
    string? Name) : IIntegrationEvent;
