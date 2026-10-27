using ClarioShop.Catalog.Domain;

namespace ClarioShop.Catalog.Tests.Domain;

public sealed class SlugTests
{
    [Theory]
    [InlineData("Chinigura Aromatic Rice", "chinigura-aromatic-rice")]
    [InlineData("  Rice & Grains  ", "rice-grains")]
    [InlineData("Pran Mango Juice (1L)", "pran-mango-juice-1l")]
    [InlineData("Teer Soybean Oil -- 5 L", "teer-soybean-oil-5-l")]
    public void From_LatinText_ReturnsLowercaseHyphenatedSlug(string text, string expected)
    {
        Assert.Equal(expected, Slug.From(text, "product"));
    }

    [Fact]
    public void From_BanglaOnlyText_FallsBackToPrefixedSlug()
    {
        var slug = Slug.From("চিনিগুড়া চাল", "product");

        Assert.Matches("^product-[0-9a-f]{8}$", slug);
    }

    [Fact]
    public void From_MixedText_KeepsLatinPart()
    {
        Assert.Equal("chinigura-1-kg", Slug.From("চিনিগুড়া Chinigura 1 kg", "product"));
    }
}
