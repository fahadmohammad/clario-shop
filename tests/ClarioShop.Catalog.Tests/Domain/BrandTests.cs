using ClarioShop.Catalog.Domain;

namespace ClarioShop.Catalog.Tests.Domain;

public sealed class BrandTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_TrimsNameAndNormalizesIgnoringCase()
    {
        var brand = Brand.Create("  Pran ", "pran", Now);

        Assert.Equal(("Pran", "PRAN", "pran"), (brand.Name, brand.NormalizedName, brand.Slug));
    }

    [Fact]
    public void Rename_ChangesNameAndKeepsSlug()
    {
        var brand = Brand.Create("Pran", "pran", Now);

        brand.Rename(" PRAN-RFL ");

        Assert.Equal(("PRAN-RFL", "PRAN-RFL", "pran"), (brand.Name, brand.NormalizedName, brand.Slug));
    }

    [Theory]
    [InlineData("Square", "square")]
    [InlineData("SQUARE ", "  square")]
    public void Normalize_SameNameInAnotherCase_IsEqual(string first, string second)
    {
        Assert.Equal(Brand.Normalize(first), Brand.Normalize(second));
    }
}
