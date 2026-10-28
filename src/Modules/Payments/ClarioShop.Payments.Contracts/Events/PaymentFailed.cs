using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Payments.Contracts.Events;

/// <summary>
/// An online payment for an order failed for good: the gateway confirms nothing was paid and the shopper stopped
/// trying. Never published from a callback or the shopper's redirect alone.
/// </summary>
/// <param name="Method">E.g. <c>SslCommerz</c>.</param>
/// <param name="Amount">What the payment asked for.</param>
/// <param name="Reason">Why it failed, e.g. what the gateway reported; for the order history.</param>
public sealed record PaymentFailed(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid PaymentId,
    Guid OrderId,
    string OrderNumber,
    string Method,
    decimal Amount,
    string Reason) : IIntegrationEvent;
