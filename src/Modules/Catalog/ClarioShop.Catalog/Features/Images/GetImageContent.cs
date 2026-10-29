using ClarioShop.Catalog.Persistence;
using ClarioShop.Infrastructure.Storage;

namespace ClarioShop.Catalog.Features.Images;

/// <summary>An image file, for the storefront and the back office alike.</summary>
public static class GetImageContent
{
    public sealed record Query(Guid ImageId);

    /// <summary>The caller disposes <paramref name="Content"/>.</summary>
    public sealed record Response(Stream Content, string ContentType);

    internal sealed class Handler(CatalogDbContext db, IFileStorage storage) : ICommandHandler<Query, Response>
    {
        public async Task<Result<Response>> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            var image = await db.ProductImages.AsNoTracking()
                .Where(i => i.Id == query.ImageId)
                .Select(i => new { i.StorageKey, i.ContentType })
                .SingleOrDefaultAsync(cancellationToken);

            var content = image is null ? null : await storage.OpenReadAsync(image.StorageKey, cancellationToken);
            return content is null ? NotFound : new Response(content, image!.ContentType);
        }

        private static Error NotFound => Error.NotFound("catalog.image_not_found", "No image has this address.");
    }
}
