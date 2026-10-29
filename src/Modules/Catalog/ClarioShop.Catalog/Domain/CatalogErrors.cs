using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Catalog.Domain;

internal static class CatalogErrors
{
    public static readonly Error ProductNotFound = Error.NotFound("catalog.product_not_found", "The product does not exist.");

    public static readonly Error VariantNotFound = Error.NotFound("catalog.variant_not_found", "The product has no such variant.");

    public static readonly Error Archived = Error.Conflict("catalog.archived", "The product is archived and can no longer be changed.");

    public static readonly Error Concurrency = Error.Conflict(
        "catalog.concurrency", "Someone else changed this product at the same time. Reload it and try again.");

    public static Error SkuTaken(string sku) => Error.Conflict("catalog.sku_taken", $"SKU {sku} is already used by another product.");
}
