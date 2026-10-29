using ClarioShop.Catalog.Domain;
using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Catalog.Tests.Domain;

public sealed class VariantAttributeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 10, 0, 0, TimeSpan.Zero);

    private static readonly Guid Size = Guid.CreateVersion7();
    private static readonly Guid Colour = Guid.CreateVersion7();
    private static readonly AttributeValueRef Small = new(Size, Guid.CreateVersion7());
    private static readonly AttributeValueRef Medium = new(Size, Guid.CreateVersion7());
    private static readonly AttributeValueRef Red = new(Colour, Guid.CreateVersion7());
    private static readonly AttributeValueRef Blue = new(Colour, Guid.CreateVersion7());

    [Fact]
    public void Create_VariantsWithDifferentValues_KeepsEachVariantsValues()
    {
        var result = Product.Create("T-Shirt", "t-shirt", null, null, null, [Spec("TEE-S-RED", Small, Red), Spec("TEE-M-RED", Medium, Red)], Now);

        var small = result.Value.Variants.Single(v => v.Sku == "TEE-S-RED");
        Assert.Equal(new[] { Small.ValueId, Red.ValueId }.Order(), small.AttributeValues.Select(v => v.AttributeValueId).Order());
    }

    [Fact]
    public void Create_TwoValuesOfOneAttribute_ReturnsValidationError()
    {
        var result = Product.Create("T-Shirt", "t-shirt", null, null, null, [Spec("TEE-1", Small, Medium)], Now);

        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Create_TwoVariantsWithSameValues_ReturnsValidationError()
    {
        var result = Product.Create("T-Shirt", "t-shirt", null, null, null, [Spec("TEE-1", Small, Red), Spec("TEE-2", Red, Small)], Now);

        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Create_VariantsWithoutValues_AreAllowed()
    {
        var result = Product.Create("Rice", "rice", null, null, null, [Spec("RICE-1"), Spec("RICE-5")], Now);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void AddVariant_SameValuesAsExistingVariant_ReturnsValidationError()
    {
        var product = Product.Create("T-Shirt", "t-shirt", null, null, null, [Spec("TEE-S-RED", Small, Red)], Now).Value;

        var result = product.AddVariant(Spec("TEE-S-RED-2", Red, Small));

        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Single(product.Variants);
    }

    [Fact]
    public void UpdateVariant_NewValues_ReplacesChangedAndRemovedOnes()
    {
        var product = Product.Create("T-Shirt", "t-shirt", null, null, null, [Spec("TEE-1", Small, Red)], Now).Value;
        var variant = product.Variants.Single();

        var result = product.UpdateVariant(variant.Id, null, null, isActive: true, [Medium]);

        Assert.True(result.IsSuccess);
        var only = Assert.Single(variant.AttributeValues);
        Assert.Equal((Size, Medium.ValueId), (only.AttributeId, only.AttributeValueId));
    }

    [Fact]
    public void UpdateVariant_ValuesOfAnotherVariant_ReturnsValidationErrorAndKeepsValues()
    {
        var product = Product.Create("T-Shirt", "t-shirt", null, null, null, [Spec("TEE-1", Small, Red), Spec("TEE-2", Small, Blue)], Now).Value;
        var second = product.Variants.Single(v => v.Sku == "TEE-2");

        var result = product.UpdateVariant(second.Id, null, null, isActive: true, [Red, Small]);

        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Contains(second.AttributeValues, v => v.AttributeValueId == Blue.ValueId);
    }

    [Fact]
    public void UpdateVariant_KeepsOwnValues_Succeeds()
    {
        var product = Product.Create("T-Shirt", "t-shirt", null, null, null, [Spec("TEE-1", Small, Red)], Now).Value;
        var variant = product.Variants.Single();

        var result = product.UpdateVariant(variant.Id, "Small red", null, isActive: true, [Small, Red]);

        Assert.True(result.IsSuccess);
    }

    private static VariantSpec Spec(string sku, params AttributeValueRef[] values) => new(sku, null, 100m, null, null, false, values);
}
