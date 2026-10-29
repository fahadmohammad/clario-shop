using ClarioShop.SharedKernel.Domain;

namespace ClarioShop.Catalog.Domain;

/// <summary>A photo of a product. The file lives in file storage under <see cref="StorageKey"/>; the row keeps its place.</summary>
internal sealed class ProductImage : Entity
{
    private ProductImage()
    {
    }

    public Guid ProductId { get; private set; }

    public string StorageKey { get; private set; } = string.Empty;

    /// <summary>Detected from the file's bytes, never taken from the uploader's word.</summary>
    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    /// <summary>Describes the photo for screen readers and when it fails to load.</summary>
    public string AltText { get; private set; } = string.Empty;

    /// <summary>Where the image sits in the gallery; the first one is the product's main image.</summary>
    public int Position { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    internal static ProductImage Create(Guid id, Guid productId, ImageSpec spec, int position, DateTimeOffset now) => new()
    {
        Id = id,
        ProductId = productId,
        StorageKey = spec.StorageKey,
        ContentType = spec.ContentType,
        SizeBytes = spec.SizeBytes,
        AltText = spec.AltText?.Trim() ?? string.Empty,
        Position = position,
        CreatedAt = now,
    };

    internal void MoveTo(int position) => Position = position;
}
