using ClarioShop.Catalog.Domain;
using ClarioShop.Catalog.Persistence;

namespace ClarioShop.Catalog.Features.Products;

public static class GetProductBySlug
{
    public sealed record Query(string Slug);

    public sealed record Response(
        Guid Id,
        string Name,
        string Slug,
        string Description,
        string? Brand,
        CategoryRef? Category,
        IReadOnlyList<VariantItem> Variants);

    public sealed record CategoryRef(string Name, string Slug);

    public sealed record VariantItem(Guid Id, string Sku, string Name, decimal Price, decimal? CompareAtPrice, bool SoldByWeight);

    internal sealed class Handler(CatalogDbContext db) : ICommandHandler<Query, Response>
    {
        public async Task<Result<Response>> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            var product = await db.Products.AsNoTracking()
                .Where(p => p.Slug == query.Slug && p.Status == ProductStatus.Published)
                .Select(p => new Response(
                    p.Id,
                    p.Name,
                    p.Slug,
                    p.Description,
                    p.Brand,
                    db.Categories.Where(c => c.Id == p.CategoryId).Select(c => new CategoryRef(c.Name, c.Slug)).FirstOrDefault(),
                    p.Variants.Where(v => v.IsActive)
                        .OrderBy(v => v.Price)
                        .Select(v => new VariantItem(v.Id, v.Sku, v.Name, v.Price, v.CompareAtPrice, v.SoldByWeight))
                        .ToList()))
                .SingleOrDefaultAsync(cancellationToken);

            return product is null
                ? Error.NotFound("catalog.product_not_found", "No published product has this address.")
                : product;
        }
    }
}
