using ClarioShop.Catalog.Contracts.Events;
using ClarioShop.Catalog.Domain;
using ClarioShop.Catalog.Persistence;

namespace ClarioShop.Catalog.Features.Products;

/// <summary>
/// Takes a product off the storefront for good. Nothing is deleted: orders, stock and reports keep pointing at
/// its variants, which are simply no longer sellable.
/// </summary>
public static class ArchiveProduct
{
    public sealed record Command(Guid ProductId);

    internal sealed class Handler(CatalogDbContext db, TimeProvider clock) : ICommandHandler<Command, ProductDetails>
    {
        public async Task<Result<ProductDetails>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var product = await db.LoadProductAsync(command.ProductId, cancellationToken);
            if (product is null)
            {
                return CatalogErrors.ProductNotFound;
            }

            var now = clock.GetUtcNow();
            var archived = product.Archive(now);
            if (archived.IsFailure)
            {
                return archived.Error;
            }

            db.Publish(new ProductArchived(Guid.CreateVersion7(), now, product.Id));
            var saved = await db.SaveProductAsync(product, cancellationToken);
            return saved.IsFailure ? saved.Error : ProductDetails.From(product);
        }
    }
}
