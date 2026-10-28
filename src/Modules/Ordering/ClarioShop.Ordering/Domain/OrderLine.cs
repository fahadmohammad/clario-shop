using ClarioShop.SharedKernel.Domain;

namespace ClarioShop.Ordering.Domain;

/// <summary>One variant on an order, with name, SKU and price copied at purchase time so catalog changes never alter history.</summary>
internal sealed class OrderLine : Entity
{
    private OrderLine()
    {
    }

    public Guid OrderId { get; private set; }

    /// <summary>1-based position on the order, in the order the shopper added the items.</summary>
    public int Position { get; private set; }

    public Guid VariantId { get; private set; }

    public Guid ProductId { get; private set; }

    public string ProductName { get; private set; } = string.Empty;

    public string VariantName { get; private set; } = string.Empty;

    public string Sku { get; private set; } = string.Empty;

    public decimal UnitPrice { get; private set; }

    /// <summary>Units, or kg for items sold by weight.</summary>
    public decimal Quantity { get; private set; }

    public decimal LineTotal { get; private set; }

    internal static OrderLine Create(Guid orderId, int position, OrderLineSpec spec) => new()
    {
        Id = Guid.CreateVersion7(),
        OrderId = orderId,
        Position = position,
        VariantId = spec.VariantId,
        ProductId = spec.ProductId,
        ProductName = spec.ProductName,
        VariantName = spec.VariantName,
        Sku = spec.Sku,
        UnitPrice = spec.UnitPrice,
        Quantity = spec.Quantity,
        LineTotal = Money.Round(spec.UnitPrice * spec.Quantity),
    };
}
