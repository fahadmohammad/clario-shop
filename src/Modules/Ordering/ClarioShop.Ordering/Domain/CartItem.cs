using ClarioShop.SharedKernel.Domain;

namespace ClarioShop.Ordering.Domain;

/// <summary>A variant and how much of it the shopper wants. Prices are never stored here; they are read from Catalog.</summary>
internal sealed class CartItem : Entity
{
    private CartItem()
    {
    }

    public Guid CartId { get; private set; }

    public Guid VariantId { get; private set; }

    /// <summary>Units, or kg for items sold by weight.</summary>
    public decimal Quantity { get; private set; }

    public DateTimeOffset AddedAt { get; private set; }

    internal static CartItem Create(Guid cartId, Guid variantId, decimal quantity, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        CartId = cartId,
        VariantId = variantId,
        Quantity = quantity,
        AddedAt = now,
    };

    internal void SetQuantity(decimal quantity) => Quantity = quantity;
}
