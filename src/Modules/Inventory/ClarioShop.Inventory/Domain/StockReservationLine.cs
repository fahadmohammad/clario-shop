namespace ClarioShop.Inventory.Domain;

internal sealed class StockReservationLine
{
    private StockReservationLine()
    {
    }

    public Guid Id { get; private set; }

    public Guid ReservationId { get; private set; }

    public Guid VariantId { get; private set; }

    public decimal Quantity { get; private set; }

    internal static StockReservationLine Create(Guid reservationId, Guid variantId, decimal quantity) => new()
    {
        Id = Guid.CreateVersion7(),
        ReservationId = reservationId,
        VariantId = variantId,
        Quantity = quantity,
    };
}
