using ClarioShop.Catalog.Domain;
using ClarioShop.Catalog.Features.Products;
using ClarioShop.Catalog.Persistence;
using ClarioShop.Infrastructure.Storage;
using ClarioShop.SharedKernel.Tenancy;

namespace ClarioShop.Catalog.Features.Images;

/// <summary>
/// Adds a photo at the end of a product's gallery. The file goes to file storage first and the row second, so a row
/// never points at a missing file; when the row cannot be saved, the file is removed again.
/// </summary>
public static class UploadProductImage
{
    /// <summary>Phone photos are a few MB; the CDN serves resized copies to shoppers.</summary>
    public const int MaxBytes = 5 * 1024 * 1024;

    public const int MaxAltTextLength = 200;

    /// <param name="Length">The file's size as declared by the upload; zero when no file was attached.</param>
    public sealed record Command(Guid ProductId, Stream Content, long Length, string? AltText);

    internal sealed class Handler(CatalogDbContext db, IFileStorage storage, ITenantContext tenant, TimeProvider clock)
        : ICommandHandler<Command, ImageDetails>
    {
        public async Task<Result<ImageDetails>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            if (command.Length == 0)
            {
                return Error.Validation("file", "Attach an image file in the form field \"file\".");
            }

            if (command.Length > MaxBytes)
            {
                return TooLarge;
            }

            if (command.AltText?.Trim().Length > MaxAltTextLength)
            {
                return Error.Validation("altText", $"Alt text can be at most {MaxAltTextLength} characters.");
            }

            var product = await db.LoadProductAsync(command.ProductId, cancellationToken);
            if (product is null)
            {
                return CatalogErrors.ProductNotFound;
            }

            await using var file = new MemoryStream();
            await command.Content.CopyToAsync(file, cancellationToken);
            if (file.Length > MaxBytes)
            {
                return TooLarge;
            }

            var format = ImageFormat.Detect(file.GetBuffer().AsSpan(0, (int)Math.Min(file.Length, ImageFormat.HeaderLength)));
            if (format is null)
            {
                return Error.Validation("file", "Upload a JPEG, PNG, WebP or AVIF image.");
            }

            var imageId = Guid.CreateVersion7();
            var key = $"{tenant.TenantId:N}/catalog/products/{product.Id:N}/{imageId:N}{format.Extension}";
            var added = product.AddImage(imageId, new ImageSpec(key, format.ContentType, file.Length, command.AltText), clock.GetUtcNow());
            if (added.IsFailure)
            {
                return added.Error;
            }

            file.Position = 0;
            await storage.SaveAsync(key, file, format.ContentType, cancellationToken);

            // Only the image row is written, not the product's: staff uploading several photos at once must not
            // turn each other's uploads into conflicts.
            var saved = false;
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                saved = true;
            }
            finally
            {
                if (!saved)
                {
                    await storage.DeleteAsync(key, CancellationToken.None);
                }
            }

            return ImageDetails.From(added.Value);
        }

        private static Error TooLarge => Error.Validation("file", $"An image can be at most {MaxBytes / 1024 / 1024} MB.");
    }
}
