using ClarioShop.Catalog.Domain;
using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Catalog.Tests.Domain;

public sealed class ProductEditingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Edit_Draft_ReplacesContentAndKeepsSlug()
    {
        var product = Create();

        var result = product.Edit("  Chinigura Premium Rice ", " New text ", null, "  ");

        Assert.True(result.IsSuccess);
        Assert.Equal("Chinigura Premium Rice", product.Name);
        Assert.Equal("New text", product.Description);
        Assert.Null(product.Brand);
        Assert.Equal("chinigura-rice", product.Slug);
    }

    [Fact]
    public void Edit_Archived_ReturnsConflict()
    {
        var product = Create();
        product.Archive(Now);

        var result = product.Edit("Other", null, null, null);

        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    [Fact]
    public void Archive_Published_SetsArchivedAt()
    {
        var product = Create();
        product.Publish(Now);

        var result = product.Archive(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(ProductStatus.Archived, product.Status);
        Assert.Equal(Now, product.ArchivedAt);
    }

    [Fact]
    public void Archive_AlreadyArchived_ReturnsConflict()
    {
        var product = Create();
        product.Archive(Now);

        var result = product.Archive(Now);

        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    [Fact]
    public void AddVariant_NewSku_AddsActiveVariantWithNormalizedSku()
    {
        var product = Create();

        var result = product.AddVariant(new VariantSpec("rice-5kg", "5 kg", 790m, 825m, [" 894 "], false));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, product.Variants.Count);
        Assert.Equal("RICE-5KG", result.Value.Sku);
        Assert.Equal(["894"], result.Value.Barcodes);
        Assert.True(result.Value.IsActive);
        Assert.Equal(product.Id, result.Value.ProductId);
    }

    [Fact]
    public void AddVariant_SkuAlreadyOnProduct_ReturnsConflict()
    {
        var product = Create();

        var result = product.AddVariant(new VariantSpec("rice-1kg", "1 kg", 170m, null, null, false));

        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(100, 100)]
    [InlineData(100, 90)]
    public void AddVariant_InvalidPrice_ReturnsValidationError(decimal price, int? compareAtPrice)
    {
        var product = Create();

        var result = product.AddVariant(new VariantSpec("RICE-2KG", "2 kg", price, compareAtPrice, null, false));

        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void AddVariant_Archived_ReturnsConflict()
    {
        var product = Create();
        product.Archive(Now);

        var result = product.AddVariant(new VariantSpec("RICE-2KG", "2 kg", 330m, null, null, false));

        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    [Fact]
    public void UpdateVariant_LastActiveVariantOfPublishedProduct_CannotBeDeactivated()
    {
        var product = Create();
        product.Publish(Now);
        var variant = product.Variants.Single();

        var result = product.UpdateVariant(variant.Id, variant.Name, null, isActive: false);

        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.True(variant.IsActive);
    }

    [Fact]
    public void UpdateVariant_AnotherVariantStaysActive_Deactivates()
    {
        var product = Create();
        var second = product.AddVariant(new VariantSpec("RICE-5KG", "5 kg", 790m, null, null, false)).Value;
        product.Publish(Now);

        var result = product.UpdateVariant(second.Id, "5 kg bag", ["111", "111"], isActive: false);

        Assert.True(result.IsSuccess);
        Assert.False(second.IsActive);
        Assert.Equal("5 kg bag", second.Name);
        Assert.Equal(["111"], second.Barcodes);
    }

    [Fact]
    public void UpdateVariant_DraftWithOneVariant_CanDeactivate()
    {
        var product = Create();
        var variant = product.Variants.Single();

        var result = product.UpdateVariant(variant.Id, null, null, isActive: false);

        Assert.True(result.IsSuccess);
        Assert.False(variant.IsActive);
    }

    [Fact]
    public void UpdateVariant_UnknownVariant_ReturnsNotFound()
    {
        var product = Create();

        var result = product.UpdateVariant(Guid.CreateVersion7(), null, null, isActive: true);

        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public void ChangePrice_NewPrice_ReturnsOldValuesAndRoundsNew()
    {
        var product = Create();
        var variant = product.Variants.Single();

        var result = product.ChangePrice(variant.Id, 171.555m, 199m);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsChange);
        Assert.Equal(165m, result.Value.OldPrice);
        Assert.Null(result.Value.OldCompareAtPrice);
        Assert.Equal(171.56m, variant.Price);
        Assert.Equal(199m, variant.CompareAtPrice);
    }

    [Fact]
    public void ChangePrice_SamePrice_IsNotAChange()
    {
        var product = Create();
        var variant = product.Variants.Single();

        var result = product.ChangePrice(variant.Id, 165.00m, null);

        Assert.False(result.Value.IsChange);
    }

    [Fact]
    public void ChangePrice_CompareAtNotAbovePrice_ReturnsValidationErrorAndKeepsPrice()
    {
        var product = Create();
        var variant = product.Variants.Single();

        var result = product.ChangePrice(variant.Id, 200m, 200m);

        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(165m, variant.Price);
    }

    [Fact]
    public void ChangePrice_Archived_ReturnsConflict()
    {
        var product = Create();
        var variant = product.Variants.Single();
        product.Archive(Now);

        var result = product.ChangePrice(variant.Id, 170m, null);

        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    private static Product Create() =>
        Product.Create("Chinigura Rice", "chinigura-rice", "Aromatic", null, "Pran", [new VariantSpec("RICE-1KG", "1 kg", 165m, null, null, false)], Now).Value;
}
