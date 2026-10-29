using ClarioShop.Catalog.Domain;
using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Catalog.Tests.Domain;

public sealed class ProductImageTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AddImage_Several_AppendsInOrderWithFirstAsMain()
    {
        var product = Create();

        var first = AddImage(product);
        var second = AddImage(product);

        Assert.Equal([first, second], product.OrderedImages().Select(i => i.Id));
        Assert.Equal([0, 1], product.OrderedImages().Select(i => i.Position));
    }

    [Fact]
    public void AddImage_TrimsAltText()
    {
        var product = Create();

        var image = product.AddImage(Guid.CreateVersion7(), new ImageSpec("k", "image/png", 10, "  Front of the pack "), Now);

        Assert.Equal("Front of the pack", image.Value.AltText);
    }

    [Fact]
    public void AddImage_BeyondLimit_ReturnsValidationError()
    {
        var product = Create();
        for (var i = 0; i < Product.MaxImages; i++)
        {
            AddImage(product);
        }

        var result = product.AddImage(Guid.CreateVersion7(), Spec(), Now);

        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void AddImage_Archived_ReturnsConflict()
    {
        var product = Create();
        product.Archive(Now);

        var result = product.AddImage(Guid.CreateVersion7(), Spec(), Now);

        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    [Fact]
    public void RemoveImage_Main_MakesNextImageMain()
    {
        var product = Create();
        var first = AddImage(product);
        var second = AddImage(product);
        var third = AddImage(product);

        var result = product.RemoveImage(first);

        Assert.True(result.IsSuccess);
        Assert.Equal([second, third], product.OrderedImages().Select(i => i.Id));
        Assert.Equal([0, 1], product.OrderedImages().Select(i => i.Position));
    }

    [Fact]
    public void RemoveImage_Unknown_ReturnsNotFound()
    {
        var product = Create();

        var result = product.RemoveImage(Guid.CreateVersion7());

        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public void ReorderImages_EveryImageOnce_SetsTheOrder()
    {
        var product = Create();
        var first = AddImage(product);
        var second = AddImage(product);
        var third = AddImage(product);

        var result = product.ReorderImages([third, first, second]);

        Assert.True(result.IsSuccess);
        Assert.Equal([third, first, second], product.OrderedImages().Select(i => i.Id));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4)]
    public void ReorderImages_NotExactlyTheGallery_ReturnsValidationError(int variation)
    {
        var product = Create();
        var first = AddImage(product);
        var second = AddImage(product);
        Guid[] ids = variation switch
        {
            1 => [first],
            3 => [first, second, Guid.CreateVersion7()],
            _ => [first, first],
        };

        var result = product.ReorderImages(ids);

        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal([first, second], product.OrderedImages().Select(i => i.Id));
    }

    [Fact]
    public void SetMainImage_MovesItToFrontAndKeepsTheRestInOrder()
    {
        var product = Create();
        var first = AddImage(product);
        var second = AddImage(product);
        var third = AddImage(product);

        var result = product.SetMainImage(third);

        Assert.True(result.IsSuccess);
        Assert.Equal([third, first, second], product.OrderedImages().Select(i => i.Id));
    }

    [Fact]
    public void SetMainImage_Archived_ReturnsConflict()
    {
        var product = Create();
        var image = AddImage(product);
        product.Archive(Now);

        var result = product.SetMainImage(image);

        Assert.Equal(ErrorType.Conflict, result.Error.Type);
    }

    private static Guid AddImage(Product product) => product.AddImage(Guid.CreateVersion7(), Spec(), Now).Value.Id;

    private static ImageSpec Spec() => new($"tenant/catalog/{Guid.NewGuid():N}.png", "image/png", 1024, null);

    private static Product Create() =>
        Product.Create("Chinigura Rice", "chinigura-rice", null, null, null, [new VariantSpec("RICE-1KG", "1 kg", 165m, null, null, false)], Now).Value;
}
