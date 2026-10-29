using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Catalog.Domain;

internal static class CatalogErrors
{
    public static readonly Error ProductNotFound = Error.NotFound("catalog.product_not_found", "The product does not exist.");

    public static readonly Error VariantNotFound = Error.NotFound("catalog.variant_not_found", "The product has no such variant.");

    public static readonly Error Archived = Error.Conflict("catalog.archived", "The product is archived and can no longer be changed.");

    public static readonly Error Concurrency = Error.Conflict(
        "catalog.concurrency", "Someone else changed this product at the same time. Reload it and try again.");

    public static readonly Error BrandNotFound = Error.NotFound("catalog.brand_not_found", "The brand does not exist.");

    public static readonly Error AttributeNotFound = Error.NotFound("catalog.attribute_not_found", "The attribute does not exist.");

    public static readonly Error AttributeValueNotFound =
        Error.NotFound("catalog.attribute_value_not_found", "The attribute has no such value.");

    public static Error AttributeNameTaken(string name) =>
        Error.Conflict("catalog.attribute_name_taken", $"An attribute named {name} already exists.");

    public static Error AttributeValueTaken(string attribute, string value) =>
        Error.Conflict("catalog.attribute_value_taken", $"{attribute} already has the value {value}.");

    public static Error BrandNameTaken(string name) => Error.Conflict("catalog.brand_name_taken", $"A brand named {name} already exists.");

    public static Error SkuTaken(string sku) => Error.Conflict("catalog.sku_taken", $"SKU {sku} is already used by another product.");
}
