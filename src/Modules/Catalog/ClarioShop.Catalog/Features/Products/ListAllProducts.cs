using ClarioShop.Catalog.Domain;
using ClarioShop.Catalog.Persistence;

namespace ClarioShop.Catalog.Features.Products;

/// <summary>Every product for the back office, drafts and archived ones included.</summary>
public static class ListAllProducts
{
    /// <param name="Search">Part of the product name, or a variant's SKU.</param>
    /// <param name="Status">Draft, Published or Archived; all when empty.</param>
    public sealed record Query(string? Search, string? Status, Guid? CategoryId, Guid? BrandId, int Page = 1, int PageSize = 50);

    /// <param name="PriceFrom">The lowest price of its active variants; null when none is active.</param>
    public sealed record ProductRow(
        Guid Id,
        string Name,
        string Slug,
        string Status,
        Guid? CategoryId,
        Guid? BrandId,
        int VariantCount,
        int ActiveVariantCount,
        decimal? PriceFrom,
        DateTimeOffset CreatedAt,
        DateTimeOffset? PublishedAt,
        DateTimeOffset? ArchivedAt);

    public sealed record Response(IReadOnlyList<ProductRow> Items, int Page, int PageSize, int TotalCount);

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
            RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
            RuleFor(query => query.Search).MaximumLength(100);
            RuleFor(query => query.Status).IsEnumName(typeof(ProductStatus), caseSensitive: false)
                .When(query => !string.IsNullOrEmpty(query.Status))
                .WithMessage("Status must be Draft, Published or Archived.");
        }
    }

    internal sealed class Handler(CatalogDbContext db) : ICommandHandler<Query, Response>
    {
        public async Task<Result<Response>> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            var products = db.Products.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                var pattern = LikePattern.Containing(search);
                var sku = Variant.NormalizeSku(search);
                products = products.Where(p => EF.Functions.ILike(p.Name, pattern, "\\") || p.Variants.Any(v => v.Sku == sku));
            }

            if (!string.IsNullOrEmpty(query.Status))
            {
                var status = Enum.Parse<ProductStatus>(query.Status, ignoreCase: true);
                products = products.Where(p => p.Status == status);
            }

            if (query.CategoryId is { } categoryId)
            {
                products = products.Where(p => p.CategoryId == categoryId);
            }

            if (query.BrandId is { } brandId)
            {
                products = products.Where(p => p.BrandId == brandId);
            }

            var total = await products.CountAsync(cancellationToken);
            var items = await products
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Slug,
                    p.Status,
                    p.CategoryId,
                    p.BrandId,
                    VariantCount = p.Variants.Count,
                    ActiveVariantCount = p.Variants.Count(v => v.IsActive),
                    PriceFrom = p.Variants.Where(v => v.IsActive).Min(v => (decimal?)v.Price),
                    p.CreatedAt,
                    p.PublishedAt,
                    p.ArchivedAt,
                })
                .ToListAsync(cancellationToken);

            return new Response(
                items.Select(p => new ProductRow(
                        p.Id,
                        p.Name,
                        p.Slug,
                        p.Status.ToString(),
                        p.CategoryId,
                        p.BrandId,
                        p.VariantCount,
                        p.ActiveVariantCount,
                        p.PriceFrom,
                        p.CreatedAt,
                        p.PublishedAt,
                        p.ArchivedAt))
                    .ToList(),
                query.Page,
                query.PageSize,
                total);
        }
    }
}
