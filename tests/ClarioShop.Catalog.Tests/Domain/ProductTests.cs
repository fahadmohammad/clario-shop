using ClarioShop.Catalog.Domain;
using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Catalog.Tests.Domain;

public sealed class ProductTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ValidVariants_CreatesDraftWithNormalizedSkus()
    {
        var result = Create(new VariantSpec("rice-1kg", "1 kg", 165m, null, ["8941100500123", " 8941100500123 "], false));

        Assert.True(result.IsSuccess);
        Assert.Equal(ProductStatus.Draft, result.Value.Status);
        var variant = Assert.Single(result.Value.Variants);
        Assert.Equal("RICE-1KG", variant.Sku);
        Assert.Equal(["8941100500123"], variant.Barcodes);
    }

    [Fact]
    public void Create_NoVariants_ReturnsValidationError()
    {
        var result = Create();

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Create_DuplicateSkuIgnoringCase_ReturnsValidationError()
    {
        var result = Create(
            new VariantSpec("RICE-1KG", "1 kg", 165m, null, null, false),
            new VariantSpec("rice-1kg", "1 kg", 170m, null, null, false));

        Assert.True(result.IsFailure);
        Assert.Contains("RICE-1KG", result.Error.ValidationErrors!["variants"][0], StringComparison.Ordinal);
    }

    [Fact]
    public void Create_CompareAtPriceNotAbovePrice_ReturnsValidationError()
    {
        var result = Create(new VariantSpec("RICE-5KG", "5 kg", 790m, 790m, null, false));

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Publish_Draft_SetsPublishedAt()
    {
        var product = Create(new VariantSpec("OIL-1L", "1 L", 189m, null, null, false)).Value;

        var result = product.Publish(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(ProductStatus.Published, product.Status);
        Assert.Equal(Now, product.PublishedAt);
    }

    [Fact]
    public void Publish_AlreadyPublished_ReturnsConflict()
    {
        var product = Create(new VariantSpec("OIL-1L", "1 L", 189m, null, null, false)).Value;
        product.Publish(Now);

        var result = product.Publish(Now);

        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    private static Result<Product> Create(params VariantSpec[] variants) =>
        Product.Create("Chinigura Rice", "chinigura-rice", null, null, null, variants, Now);
}
