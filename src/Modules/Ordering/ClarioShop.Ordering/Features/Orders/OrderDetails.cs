using ClarioShop.Ordering.Domain;

namespace ClarioShop.Ordering.Features.Orders;

/// <summary>An order as the shopper's confirmation and the staff endpoints show it.</summary>
public sealed record OrderDetails(
    Guid Id,
    string Number,
    string Status,
    string PaymentMethod,
    Guid CustomerId,
    string CustomerName,
    string CustomerPhone,
    DeliveryAddressItem DeliveryAddress,
    string? Note,
    IReadOnlyList<OrderLineItem> Lines,
    DeliveryZoneItem? DeliveryZone,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal VatRatePercent,
    decimal Vat,
    decimal Total,
    DateTimeOffset PlacedAt,
    IReadOnlyList<StatusChangeItem> History)
{
    internal static OrderDetails From(Order order) => new(
        order.Id,
        OrderNumber.Format(order.Number),
        order.Status.ToString(),
        order.PaymentMethod.ToString(),
        order.CustomerId,
        order.CustomerName,
        order.CustomerPhone,
        new DeliveryAddressItem(
            order.DeliveryAddress.Line1,
            order.DeliveryAddress.Line2,
            order.DeliveryAddress.Area,
            order.DeliveryAddress.City,
            order.DeliveryAddress.PostalCode),
        order.Note,
        [.. order.Lines.OrderBy(line => line.Position).Select(OrderLineItem.From)],
        order.DeliveryZoneId is { } zoneId ? new DeliveryZoneItem(zoneId, order.DeliveryZoneName!) : null,
        order.Subtotal,
        order.DeliveryFee,
        order.VatRatePercent,
        order.Vat,
        order.Total,
        order.PlacedAt,
        [.. order.History.OrderBy(change => change.ChangedAt).ThenBy(change => change.Id).Select(StatusChangeItem.From)]);
}

public sealed record DeliveryAddressItem(string Line1, string? Line2, string Area, string City, string? PostalCode);

public sealed record DeliveryZoneItem(Guid Id, string Name);

public sealed record OrderLineItem(
    Guid VariantId,
    Guid ProductId,
    string ProductName,
    string VariantName,
    string Sku,
    decimal UnitPrice,
    decimal Quantity,
    decimal LineTotal)
{
    internal static OrderLineItem From(OrderLine line) => new(
        line.VariantId,
        line.ProductId,
        line.ProductName,
        line.VariantName,
        line.Sku,
        line.UnitPrice,
        line.Quantity,
        line.LineTotal);
}

public sealed record StatusChangeItem(string? FromStatus, string ToStatus, DateTimeOffset ChangedAt, Guid? ChangedByUserId, string? Note)
{
    internal static StatusChangeItem From(OrderStatusChange change) => new(
        change.FromStatus?.ToString(),
        change.ToStatus.ToString(),
        change.ChangedAt,
        change.ChangedByUserId,
        change.Note);
}
