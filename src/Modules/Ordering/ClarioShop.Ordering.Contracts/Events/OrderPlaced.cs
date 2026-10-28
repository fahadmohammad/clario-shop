using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Ordering.Contracts.Events;

/// <summary>A shopper placed an order; its stock is held by <paramref name="ReservationId"/> until it is confirmed or cancelled.</summary>
/// <param name="OrderNumber">The readable per-tenant number, e.g. <c>CS-000123</c>.</param>
/// <param name="PaymentMethod">E.g. <c>CashOnDelivery</c>.</param>
/// <param name="Total">What the shopper pays: <paramref name="Subtotal"/> plus <paramref name="DeliveryFee"/> plus <paramref name="Vat"/>.</param>
public sealed record OrderPlaced(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid OrderId,
    string OrderNumber,
    Guid CustomerId,
    string PaymentMethod,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal Vat,
    decimal Total,
    Guid ReservationId,
    IReadOnlyList<OrderPlacedLine> Lines) : IIntegrationEvent;

/// <param name="Quantity">Units, or kg for items sold by weight.</param>
public sealed record OrderPlacedLine(Guid VariantId, string Sku, decimal Quantity, decimal UnitPrice);
