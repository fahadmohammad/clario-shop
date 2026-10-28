namespace ClarioShop.Ordering.Domain;

/// <summary>The delivery zone chosen at checkout, as Settings priced it then.</summary>
/// <param name="FreeDeliveryThreshold">Subtotal from which delivery is free; null when it never is.</param>
internal sealed record OrderDelivery(Guid ZoneId, string ZoneName, decimal Fee, decimal? FreeDeliveryThreshold);
