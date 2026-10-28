namespace ClarioShop.Reporting.Domain;

/// <summary>One variant on a <see cref="SalesOrder"/>, with the name, SKU and price it was sold under.</summary>
internal sealed class SalesOrderLine
{
    private SalesOrderLine()
    {
    }

    public Guid OrderId { get; private set; }

    /// <summary>1-based, as on the order.</summary>
    public int Position { get; private set; }

    public Guid VariantId { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public string ProductName { get; private set; } = string.Empty;

    public string VariantName { get; private set; } = string.Empty;

    /// <summary>Units, or kg for items sold by weight.</summary>
    public decimal Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal LineTotal { get; private set; }

    internal static SalesOrderLine Create(Guid orderId, int position, SalesOrderLineSpec spec) => new()
    {
        OrderId = orderId,
        Position = position,
        VariantId = spec.VariantId,
        Sku = spec.Sku,
        ProductName = spec.ProductName,
        VariantName = spec.VariantName,
        Quantity = spec.Quantity,
        UnitPrice = spec.UnitPrice,
        LineTotal = spec.LineTotal,
    };
}
