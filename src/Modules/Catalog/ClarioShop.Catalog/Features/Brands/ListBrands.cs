using ClarioShop.Catalog.Persistence;

namespace ClarioShop.Catalog.Features.Brands;

public static class ListBrands
{
    public sealed record Query;

    internal sealed class Handler(CatalogDbContext db) : ICommandHandler<Query, IReadOnlyList<BrandItem>>
    {
        public async Task<Result<IReadOnlyList<BrandItem>>> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            var brands = await db.Brands.AsNoTracking()
                .OrderBy(brand => brand.NormalizedName)
                .Select(brand => new BrandItem(brand.Id, brand.Name, brand.Slug))
                .ToListAsync(cancellationToken);

            return brands;
        }
    }
}
