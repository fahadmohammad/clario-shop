using ClarioShop.Catalog.Contracts;
using ClarioShop.Catalog.Domain;
using ClarioShop.Catalog.Persistence;

namespace ClarioShop.Catalog.Services;

internal sealed class CatalogQueries(CatalogDbContext db) : ICatalogQueries
{
    public async Task<IReadOnlyList<VariantSummary>> GetVariantsAsync(IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken) =>
        await (from variant in db.Variants.AsNoTracking()
               join product in db.Products.AsNoTracking() on variant.ProductId equals product.Id
               where variantIds.Contains(variant.Id)
               select new VariantSummary(
                   variant.Id,
                   product.Id,
                   product.Name,
                   variant.Sku,
                   variant.Name,
                   variant.Price,
                   variant.IsActive && product.Status == ProductStatus.Published,
                   variant.SoldByWeight))
            .ToListAsync(cancellationToken);
}
