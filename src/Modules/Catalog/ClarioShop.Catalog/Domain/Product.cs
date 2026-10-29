using ClarioShop.SharedKernel.Domain;
using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Catalog.Domain;

internal sealed class Product : Entity
{
    /// <summary>Enough for a gallery; keeps a product page light on mobile data.</summary>
    public const int MaxImages = 10;

    private static readonly Error OneValuePerAttributeError =
        Error.Validation("attributeValueIds", "A variant can have only one value per attribute.");

    private static readonly Error SameValuesError =
        Error.Validation("attributeValueIds", "Another variant of this product already has the same attribute values.");

    private readonly List<Variant> variants = [];

    private readonly List<ProductImage> images = [];

    private Product()
    {
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Set once at creation, so storefront links keep working when the product is renamed.</summary>
    public string Slug { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public Guid? CategoryId { get; private set; }

    public Guid? BrandId { get; private set; }

    public ProductStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public DateTimeOffset? ArchivedAt { get; private set; }

    /// <summary>Concurrency token (PostgreSQL xmin).</summary>
    public uint RowVersion { get; private set; }

    public IReadOnlyCollection<Variant> Variants => variants;

    public IReadOnlyCollection<ProductImage> Images => images;

    public static Result<Product> Create(
        string name,
        string slug,
        string? description,
        Guid? categoryId,
        Guid? brandId,
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
            .GroupBy(spec => Variant.NormalizeSku(spec.Sku))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateSku is not null)
        {
            return Error.Validation("variants", $"SKU {duplicateSku.Key} is used twice.");
        }

        if (variantSpecs.Any(spec => !OneValuePerAttribute(spec.AttributeValues ?? [])))
        {
            return OneValuePerAttributeError;
        }

        var sameValues = variantSpecs
            .Select(spec => Variant.CombinationKey((spec.AttributeValues ?? []).Select(value => value.ValueId)))
            .Where(key => key.Length > 0)
            .GroupBy(key => key)
            .Any(group => group.Count() > 1);
        if (sameValues)
        {
            return SameValuesError;
        }

        var product = new Product
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            Slug = slug,
            Description = description?.Trim() ?? string.Empty,
            CategoryId = categoryId,
            BrandId = brandId,
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

    /// <summary>Changes the shared content. The slug stays, so links already shared keep working.</summary>
    public Result Edit(string name, string? description, Guid? categoryId, Guid? brandId)
    {
        if (Status == ProductStatus.Archived)
        {
            return CatalogErrors.Archived;
        }

        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        CategoryId = categoryId;
        BrandId = brandId;
        return Result.Success();
    }

    /// <summary>
    /// Takes the product off the storefront for good. Its variants stay, so orders, stock and reports that point
    /// at them keep their history; they are no longer sellable.
    /// </summary>
    public Result Archive(DateTimeOffset now)
    {
        if (Status == ProductStatus.Archived)
        {
            return Error.Conflict("catalog.already_archived", "The product is already archived.");
        }

        Status = ProductStatus.Archived;
        ArchivedAt = now;
        return Result.Success();
    }

    public Result<Variant> AddVariant(VariantSpec spec)
    {
        if (Status == ProductStatus.Archived)
        {
            return CatalogErrors.Archived;
        }

        var priceError = ValidatePrice(spec.Price, spec.CompareAtPrice);
        if (priceError is not null)
        {
            return priceError;
        }

        var sku = Variant.NormalizeSku(spec.Sku);
        if (variants.Any(variant => variant.Sku == sku))
        {
            return CatalogErrors.SkuTaken(sku);
        }

        var attributeError = ValidateAttributeValues(spec.AttributeValues ?? [], exceptVariantId: null);
        if (attributeError is not null)
        {
            return attributeError;
        }

        var added = Variant.Create(Id, spec);
        variants.Add(added);
        return added;
    }

    /// <summary>
    /// Renames a variant, replaces its barcodes and attribute values, or takes it off sale. Its SKU and unit never
    /// change.
    /// </summary>
    public Result<Variant> UpdateVariant(
        Guid variantId, string? name, IReadOnlyList<string>? barcodes, bool isActive, IReadOnlyList<AttributeValueRef> attributeValues)
    {
        if (Status == ProductStatus.Archived)
        {
            return CatalogErrors.Archived;
        }

        var variant = variants.FirstOrDefault(v => v.Id == variantId);
        if (variant is null)
        {
            return CatalogErrors.VariantNotFound;
        }

        if (!isActive && Status == ProductStatus.Published && !variants.Any(v => v.IsActive && v.Id != variantId))
        {
            return Error.Validation(
                "isActive", "A published product needs an active variant. Archive the product to take it off the storefront.");
        }

        var attributeError = ValidateAttributeValues(attributeValues, variantId);
        if (attributeError is not null)
        {
            return attributeError;
        }

        variant.Update(name, barcodes, isActive, attributeValues);
        return variant;
    }

    public Result<PriceChange> ChangePrice(Guid variantId, decimal price, decimal? compareAtPrice)
    {
        if (Status == ProductStatus.Archived)
        {
            return CatalogErrors.Archived;
        }

        var variant = variants.FirstOrDefault(v => v.Id == variantId);
        if (variant is null)
        {
            return CatalogErrors.VariantNotFound;
        }

        var priceError = ValidatePrice(price, compareAtPrice);
        if (priceError is not null)
        {
            return priceError;
        }

        var change = new PriceChange(variant, variant.Price, variant.CompareAtPrice);
        variant.SetPrice(price, compareAtPrice);
        return change;
    }

    /// <summary>The gallery in order; the first image is the main one.</summary>
    public IReadOnlyList<ProductImage> OrderedImages() =>
        [.. images.OrderBy(image => image.Position).ThenBy(image => image.Id)];

    /// <summary>Adds an image at the end of the gallery; the first image a product gets becomes its main image.</summary>
    public Result<ProductImage> AddImage(Guid imageId, ImageSpec spec, DateTimeOffset now)
    {
        if (Status == ProductStatus.Archived)
        {
            return CatalogErrors.Archived;
        }

        if (images.Count >= MaxImages)
        {
            return Error.Validation("file", $"A product can have at most {MaxImages} images. Remove one first.");
        }

        var position = images.Count == 0 ? 0 : images.Max(image => image.Position) + 1;
        var image = ProductImage.Create(imageId, Id, spec, position, now);
        images.Add(image);
        return image;
    }

    /// <summary>Removes an image; the next one becomes the main image when it was the main one.</summary>
    public Result<ProductImage> RemoveImage(Guid imageId)
    {
        var image = FindImage(imageId);
        if (image.IsFailure)
        {
            return image.Error;
        }

        images.Remove(image.Value);
        Renumber(OrderedImages());
        return image.Value;
    }

    /// <summary>Puts the gallery in the given order, which must name every image of the product once.</summary>
    public Result ReorderImages(IReadOnlyList<Guid> imageIds)
    {
        if (Status == ProductStatus.Archived)
        {
            return CatalogErrors.Archived;
        }

        if (imageIds.Count != images.Count || imageIds.Distinct().Count() != imageIds.Count || images.Any(image => !imageIds.Contains(image.Id)))
        {
            return Error.Validation("imageIds", "List every image of the product exactly once.");
        }

        Renumber([.. imageIds.Select(id => images.Single(image => image.Id == id))]);
        return Result.Success();
    }

    /// <summary>Moves an image to the front of the gallery; the others keep their order behind it.</summary>
    public Result SetMainImage(Guid imageId)
    {
        var image = FindImage(imageId);
        if (image.IsFailure)
        {
            return image.Error;
        }

        Renumber([image.Value, .. OrderedImages().Where(other => other != image.Value)]);
        return Result.Success();
    }

    private Result<ProductImage> FindImage(Guid imageId)
    {
        if (Status == ProductStatus.Archived)
        {
            return CatalogErrors.Archived;
        }

        var image = images.FirstOrDefault(i => i.Id == imageId);
        return image is null ? CatalogErrors.ImageNotFound : image;
    }

    private static void Renumber(IReadOnlyList<ProductImage> ordered)
    {
        for (var position = 0; position < ordered.Count; position++)
        {
            ordered[position].MoveTo(position);
        }
    }

    private static bool OneValuePerAttribute(IReadOnlyList<AttributeValueRef> values) =>
        values.Select(value => value.AttributeId).Distinct().Count() == values.Count;

    /// <summary>
    /// A shopper picks a variant by its values, so each variant has at most one value per attribute and no two
    /// variants of the product share the same values.
    /// </summary>
    private Error? ValidateAttributeValues(IReadOnlyList<AttributeValueRef> values, Guid? exceptVariantId)
    {
        if (!OneValuePerAttribute(values))
        {
            return OneValuePerAttributeError;
        }

        var key = Variant.CombinationKey(values.Select(value => value.ValueId));
        return key.Length > 0 && variants.Any(variant => variant.Id != exceptVariantId && variant.CombinationKey() == key)
            ? SameValuesError
            : null;
    }

    private static Error? ValidatePrice(decimal price, decimal? compareAtPrice)
    {
        if (price <= 0)
        {
            return Error.Validation("price", "The price must be above zero.");
        }

        return compareAtPrice is { } compare && compare <= price
            ? Error.Validation("compareAtPrice", "A compare-at price must be higher than the price.")
            : null;
    }
}
