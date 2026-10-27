using ClarioShop.SharedKernel.Domain;
using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Catalog.Domain;

internal sealed class Product : Entity
{
    private readonly List<Variant> variants = [];

    private Product()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public Guid? CategoryId { get; private set; }

    public string? Brand { get; private set; }

    public ProductStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public IReadOnlyCollection<Variant> Variants => variants;

    public static Result<Product> Create(
        string name,
        string slug,
        string? description,
        Guid? categoryId,
        string? brand,
        IReadOnlyList<VariantSpec> variantSpecs,
        DateTimeOffset now)
    {
        if (variantSpecs.Count == 0)
        {
            return Error.Validation("variants", "A product needs at least one variant.");
        }

        if (variantSpecs.Any(spec => spec.Price <= 0))
        {
            return Error.Validation("variants", "Every variant needs a price above zero.");
        }

        if (variantSpecs.Any(spec => spec.CompareAtPrice is { } compare && compare <= spec.Price))
        {
            return Error.Validation("variants", "A compare-at price must be higher than the price.");
        }

        var duplicateSku = variantSpecs
            .GroupBy(spec => spec.Sku.Trim().ToUpperInvariant())
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateSku is not null)
        {
            return Error.Validation("variants", $"SKU {duplicateSku.Key} is used twice.");
        }

        var product = new Product
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            Slug = slug,
            Description = description?.Trim() ?? string.Empty,
            CategoryId = categoryId,
            Brand = string.IsNullOrWhiteSpace(brand) ? null : brand.Trim(),
            Status = ProductStatus.Draft,
            CreatedAt = now,
        };

        product.variants.AddRange(variantSpecs.Select(spec => Variant.Create(product.Id, spec)));
        return product;
    }

    public Result Publish(DateTimeOffset now)
    {
        if (Status == ProductStatus.Published)
        {
            return Error.Conflict("catalog.already_published", "The product is already published.");
        }

        if (Status == ProductStatus.Archived)
        {
            return Error.Conflict("catalog.archived", "An archived product cannot be published.");
        }

        if (!variants.Any(variant => variant.IsActive))
        {
            return Error.Validation("variants", "A product needs an active variant before it is published.");
        }

        Status = ProductStatus.Published;
        PublishedAt = now;
        return Result.Success();
    }
}
