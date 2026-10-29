using ClarioShop.Catalog.Domain;

namespace ClarioShop.Catalog.Features.Products;

/// <summary>A product as staff see it: any status, every variant including inactive ones.</summary>
public sealed record ProductDetails(
    Guid Id,
    string Name,
    string Slug,
    string Description,
    string? Brand,
    Guid? CategoryId,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ArchivedAt,
    IReadOnlyList<VariantDetails> Variants)
{
    internal static ProductDetails From(Product product) => new(
        product.Id,
        product.Name,
        product.Slug,
        product.Description,
        product.Brand,
        product.CategoryId,
        product.Status.ToString(),
        product.CreatedAt,
        product.PublishedAt,
        product.ArchivedAt,
        product.Variants.OrderBy(v => v.Id).Select(VariantDetails.From).ToList());
}
