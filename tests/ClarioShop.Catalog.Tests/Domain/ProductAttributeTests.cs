using ClarioShop.Catalog.Domain;
using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Catalog.Tests.Domain;

public sealed class ProductAttributeTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_TrimsNameAndKeepsValuesInGivenOrder()
    {
        var attribute = ProductAttribute.Create(" Size ", ["S", " M ", "L"], Now).Value;

        Assert.Equal(("Size", "SIZE"), (attribute.Name, attribute.NormalizedName));
        Assert.Equal(["S", "M", "L"], attribute.Values.OrderBy(v => v.Position).Select(v => v.Value));
        Assert.All(attribute.Values, value => Assert.Equal(attribute.Id, value.AttributeId));
    }

    [Fact]
    public void Create_SameValueTwiceInAnotherCase_ReturnsValidationError()
    {
        var result = ProductAttribute.Create("Colour", ["Red", " RED"], Now);

        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void AddValue_NewValue_GoesAfterExistingOnes()
    {
        var attribute = ProductAttribute.Create("Size", ["S", "M"], Now).Value;

        var added = attribute.AddValue(" XL ");

        Assert.Equal(("XL", 2), (added.Value.Value, added.Value.Position));
    }

    [Fact]
    public void AddValue_ExistingValueInAnotherCase_ReturnsConflict()
    {
        var attribute = ProductAttribute.Create("Size", ["XL"], Now).Value;

        var result = attribute.AddValue("xl");

        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Single(attribute.Values);
    }

    [Fact]
    public void RenameValue_KeepsIdAndPosition()
    {
        var attribute = ProductAttribute.Create("Colour", ["Red", "Blu"], Now).Value;
        var blue = attribute.Values.Single(v => v.Value == "Blu");

        var result = attribute.RenameValue(blue.Id, "Blue");

        Assert.True(result.IsSuccess);
        Assert.Equal(("Blue", "BLUE", 1), (blue.Value, blue.NormalizedValue, blue.Position));
    }

    [Fact]
    public void RenameValue_ToOwnNameInAnotherCase_Succeeds()
    {
        var attribute = ProductAttribute.Create("Colour", ["red"], Now).Value;
        var red = attribute.Values.Single();

        var result = attribute.RenameValue(red.Id, "Red");

        Assert.True(result.IsSuccess);
        Assert.Equal("Red", red.Value);
    }

    [Fact]
    public void RenameValue_ToAnotherValuesName_ReturnsConflict()
    {
        var attribute = ProductAttribute.Create("Colour", ["Red", "Blue"], Now).Value;
        var blue = attribute.Values.Single(v => v.Value == "Blue");

        var result = attribute.RenameValue(blue.Id, "RED");

        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    [Fact]
    public void RenameValue_UnknownValue_ReturnsNotFound()
    {
        var attribute = ProductAttribute.Create("Colour", ["Red"], Now).Value;

        var result = attribute.RenameValue(Guid.CreateVersion7(), "Blue");

        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }
}
