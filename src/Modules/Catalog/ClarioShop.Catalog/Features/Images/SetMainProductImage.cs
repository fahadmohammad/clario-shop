using ClarioShop.Catalog.Domain;
using ClarioShop.Catalog.Features.Products;
using ClarioShop.Catalog.Persistence;

namespace ClarioShop.Catalog.Features.Images;

/// <summary>Makes an image the product's main one (the storefront card and the first photo of the gallery).</summary>
public static class SetMainProductImage
{
    public sealed record Command(Guid ProductId, Guid ImageId);

    internal sealed class Handler(CatalogDbContext db) : ICommandHandler<Command, IReadOnlyList<ImageDetails>>
    {
        public async Task<Result<IReadOnlyList<ImageDetails>>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var product = await db.LoadProductAsync(command.ProductId, cancellationToken);
            if (product is null)
            {
                return CatalogErrors.ProductNotFound;
            }

            var moved = product.SetMainImage(command.ImageId);
            if (moved.IsFailure)
            {
                return moved.Error;
            }

            var saved = await db.SaveProductAsync(product, cancellationToken);
            return saved.IsFailure ? saved.Error : Result.Success(ImageDetails.ListOf(product));
        }
    }
}
