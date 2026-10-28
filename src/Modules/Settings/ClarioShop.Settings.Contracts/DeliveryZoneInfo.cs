namespace ClarioShop.Settings.Contracts;

/// <param name="Areas">Areas or districts the zone covers, e.g. <c>Dhanmondi</c>, <c>Mirpur</c>.</param>
/// <param name="Fee">Delivery fee in BDT.</param>
/// <param name="FreeDeliveryThreshold">Order subtotal (BDT) from which delivery is free; null when it never is.</param>
public sealed record DeliveryZoneInfo(
    Guid Id,
    string Name,
    IReadOnlyList<string> Areas,
    decimal Fee,
    decimal? FreeDeliveryThreshold);
