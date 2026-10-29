namespace ClarioShop.Catalog.Domain;

/// <summary>An image file already in storage, to be added to a product.</summary>
internal sealed record ImageSpec(string StorageKey, string ContentType, long SizeBytes, string? AltText);
