namespace ClarioShop.Ordering.Domain;

/// <summary>
/// What an order costs on top of its goods. VAT is charged on the goods subtotal only, at the store's rate when the
/// order is placed; delivery is the zone's fee unless the subtotal reaches the zone's free-delivery threshold.
/// </summary>
internal sealed record OrderCharges(decimal Subtotal, decimal DeliveryFee, decimal VatRatePercent, decimal Vat)
{
    public decimal Total => Subtotal + DeliveryFee + Vat;

    public static OrderCharges Calculate(decimal subtotal, OrderDelivery? delivery, decimal vatRatePercent)
    {
        var deliveryFee = delivery switch
        {
            null => 0m,
            { FreeDeliveryThreshold: { } threshold } when subtotal >= threshold => 0m,
            _ => delivery.Fee,
        };

        return new OrderCharges(subtotal, deliveryFee, vatRatePercent, Money.Round(subtotal * vatRatePercent / 100m));
    }
}
