using ClarioShop.Catalog.Domain;
using ClarioShop.Catalog.Features.Images;
using ClarioShop.Catalog.Persistence;

namespace ClarioShop.Catalog.Features.Products;

public static class ListProducts
{
    public sealed record Query(string? Search, string? Category, string? Brand, int Page = 1, int PageSize = 24);

    /// <param name="ImageUrl">The main image; null when the product has none.</param>
    public sealed record ProductCard(Guid Id, string Name, string Slug, string? Brand, decimal PriceFrom, decimal? CompareAtPrice, string? ImageUrl);

    public sealed record Response(IReadOnlyList<ProductCard> Items, int Page, int PageSize, int TotalCount);

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
            RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
            RuleFor(query => query.Search).MaximumLength(100);
            RuleFor(query => query.Category).MaximumLength(160);
            RuleFor(query => query.Brand).MaximumLength(160);
        }
    }

    internal sealed class Handler(CatalogDbContext db) : ICommandHandler<Query, Response>
    {
        public async Task<Result<Response>> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            var products = db.Products.AsNoTracking().Where(p => p.Status == ProductStatus.Published);

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var pattern = LikePattern.Containing(query.Search.Trim());
                products = products.Where(p => EF.Functions.ILike(p.Name, pattern, "\\"));
            }

            if (!string.IsNullOrWhiteSpace(query.Category))
            {
                products = products.Where(p => db.Categories.Any(c => c.Id == p.CategoryId && c.Slug == query.Category));
            }

            if (!string.IsNullOrWhiteSpace(query.Brand))
            {
                products = products.Where(p => db.Brands.Any(b => b.Id == p.BrandId && b.Slug == query.Brand));
            }

            var total = await products.CountAsync(cancellationToken);
            var items = await products
                .OrderByDescending(p => p.PublishedAt)
                .ThenBy(p => p.Id)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Slug,
                    Brand = db.Brands.Where(b => b.Id == p.BrandId).Select(b => b.Name).FirstOrDefault(),
                    PriceFrom = p.Variants.Where(v => v.IsActive).Min(v => v.Price),
                    CompareAtPrice = p.Variants.Where(v => v.IsActive).OrderBy(v => v.Price).Select(v => v.CompareAtPrice).FirstOrDefault(),
                    MainImageId = p.Images.OrderBy(i => i.Position).ThenBy(i => i.Id).Select(i => (Guid?)i.Id).FirstOrDefault(),
                })
                .ToListAsync(cancellationToken);

            var cards = items
                .Select(p => new ProductCard(
                    p.Id,
                    p.Name,
                    p.Slug,
                    p.Brand,
                    p.PriceFrom,
                    p.CompareAtPrice,
                    p.MainImageId is { } imageId ? ImageDetails.UrlFor(imageId) : null))
                .ToList();
            return new Response(cards, query.Page, query.PageSize, total);
        }
    }
}
