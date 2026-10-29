using ClarioShop.Catalog.Domain;
using ClarioShop.Catalog.Persistence;

namespace ClarioShop.Catalog.Features.Products;

/// <summary>Any product by id, drafts and archived ones included, for the back office.</summary>
public static class GetProduct
{
    public sealed record Query(Guid ProductId);

    internal sealed class Handler(CatalogDbContext db) : ICommandHandler<Query, ProductDetails>
    {
        public async Task<Result<ProductDetails>> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            var product = await db.Products.AsNoTracking().Include(p => p.Variants).Include(p => p.Images).AsSplitQuery()
                .SingleOrDefaultAsync(p => p.Id == query.ProductId, cancellationToken);
            return product is null ? CatalogErrors.ProductNotFound : ProductDetails.From(product);
        }
    }
}
