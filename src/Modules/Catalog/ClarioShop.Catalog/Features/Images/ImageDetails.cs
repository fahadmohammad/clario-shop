using ClarioShop.Catalog.Domain;

namespace ClarioShop.Catalog.Features.Images;

/// <summary>A product image as staff see it, in gallery order; the first is the main image.</summary>
public sealed record ImageDetails(Guid Id, string Url, string AltText, string ContentType, long SizeBytes)
{
    /// <summary>
    /// Images are always served through the API by id, so the address stays the same whichever storage holds the
    /// file; the response is cacheable for good, so the CDN in front of the shop serves repeat requests.
    /// </summary>
    internal static string UrlFor(Guid imageId) => $"/api/catalog/images/{imageId}";

    internal static ImageDetails From(ProductImage image) =>
        new(image.Id, UrlFor(image.Id), image.AltText, image.ContentType, image.SizeBytes);

    internal static IReadOnlyList<ImageDetails> ListOf(Product product) => [.. product.OrderedImages().Select(From)];
}
