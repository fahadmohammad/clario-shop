using ClarioShop.Catalog.Domain;

namespace ClarioShop.Catalog.Features.Products;

public sealed record VariantDetails(
    Guid Id,
    string Sku,
    string Name,
    decimal Price,
    decimal? CompareAtPrice,
    IReadOnlyList<string> Barcodes,
    bool SoldByWeight,
    bool IsActive)
{
    internal static VariantDetails From(Variant variant) => new(
        variant.Id,
        variant.Sku,
        variant.Name,
        variant.Price,
        variant.CompareAtPrice,
        variant.Barcodes,
        variant.SoldByWeight,
        variant.IsActive);
}
