namespace ClarioShop.Ordering.Domain;

/// <summary>A line to copy onto a new order: the catalog's name, SKU and price at checkout time.</summary>
internal sealed record OrderLineSpec(
    Guid VariantId,
    Guid ProductId,
    string ProductName,
    string VariantName,
    string Sku,
    decimal UnitPrice,
    decimal Quantity);
