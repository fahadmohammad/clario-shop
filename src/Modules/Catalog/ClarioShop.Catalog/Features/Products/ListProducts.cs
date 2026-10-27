using ClarioShop.Catalog.Domain;
using ClarioShop.Catalog.Persistence;

namespace ClarioShop.Catalog.Features.Products;

public static class ListProducts
{
    public sealed record Query(string? Search, string? Category, int Page = 1, int PageSize = 24);

    public sealed record ProductCard(Guid Id, string Name, string Slug, string? Brand, decimal PriceFrom, decimal? CompareAtPrice);

    public sealed record Response(IReadOnlyList<ProductCard> Items, int Page, int PageSize, int TotalCount);

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
            RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
            RuleFor(query => query.Search).MaximumLength(100);
        }
    }

    internal sealed class Handler(CatalogDbContext db) : ICommandHandler<Query, Response>
    {
        public async Task<Result<Response>> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            var products = db.Products.AsNoTracking().Where(p => p.Status == ProductStatus.Published);

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var pattern = $"%{EscapeLike(query.Search.Trim())}%";
                products = products.Where(p => EF.Functions.ILike(p.Name, pattern, "\\"));
            }

            if (!string.IsNullOrWhiteSpace(query.Category))
            {
                products = products.Where(p => db.Categories.Any(c => c.Id == p.CategoryId && c.Slug == query.Category));
            }

            var total = await products.CountAsync(cancellationToken);
            var items = await products
                .OrderByDescending(p => p.PublishedAt)
                .ThenBy(p => p.Id)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(p => new ProductCard(
                    p.Id,
                    p.Name,
                    p.Slug,
                    p.Brand,
                    p.Variants.Where(v => v.IsActive).Min(v => v.Price),
                    p.Variants.Where(v => v.IsActive).OrderBy(v => v.Price).Select(v => v.CompareAtPrice).FirstOrDefault()))
                .ToListAsync(cancellationToken);

            return new Response(items, query.Page, query.PageSize, total);
        }

        private static string EscapeLike(string value) =>
            value.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal);
    }
}
