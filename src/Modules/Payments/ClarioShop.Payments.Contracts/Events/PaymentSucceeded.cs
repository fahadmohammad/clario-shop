using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Payments.Contracts.Events;

/// <summary>Money for an order was received. An order may have several payments, e.g. part prepaid and part cash on delivery.</summary>
/// <param name="Method">E.g. <c>CashOnDelivery</c>.</param>
/// <param name="Amount">This payment only, not the order's total paid so far.</param>
public sealed record PaymentSucceeded(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid PaymentId,
    Guid OrderId,
    string OrderNumber,
    string Method,
    decimal Amount,
    DateTimeOffset PaidAt) : IIntegrationEvent;
