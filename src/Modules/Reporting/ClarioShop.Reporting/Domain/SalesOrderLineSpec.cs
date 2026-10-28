namespace ClarioShop.Reporting.Domain;

internal sealed record SalesOrderLineSpec(
    Guid VariantId,
    string Sku,
    string ProductName,
    string VariantName,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal);
