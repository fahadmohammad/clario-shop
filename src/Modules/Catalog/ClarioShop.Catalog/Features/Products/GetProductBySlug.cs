using ClarioShop.Catalog.Domain;
using ClarioShop.Catalog.Persistence;

namespace ClarioShop.Catalog.Features.Products;

public static class GetProductBySlug
{
    public sealed record Query(string Slug);

    /// <param name="Options">
    /// The attributes the product's variants differ by, each with the values its sellable variants have, in the
    /// order staff set; the shopper picks one value per option to land on a variant.
    /// </param>
    public sealed record Response(
        Guid Id,
        string Name,
        string Slug,
        string Description,
        BrandRef? Brand,
        CategoryRef? Category,
        IReadOnlyList<OptionItem> Options,
        IReadOnlyList<VariantItem> Variants);

    public sealed record BrandRef(string Name, string Slug);

    public sealed record CategoryRef(string Name, string Slug);

    public sealed record OptionItem(string Name, IReadOnlyList<string> Values);

    public sealed record VariantItem(
        Guid Id,
        string Sku,
        string Name,
        decimal Price,
        decimal? CompareAtPrice,
        bool SoldByWeight,
        IReadOnlyList<VariantAttribute> Attributes);

    public sealed record VariantAttribute(string Name, string Value);

    internal sealed class Handler(CatalogDbContext db) : ICommandHandler<Query, Response>
    {
        public async Task<Result<Response>> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            var product = await db.Products.AsNoTracking()
                .Where(p => p.Slug == query.Slug && p.Status == ProductStatus.Published)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Slug,
                    p.Description,
                    Brand = db.Brands.Where(b => b.Id == p.BrandId).Select(b => new BrandRef(b.Name, b.Slug)).FirstOrDefault(),
                    Category = db.Categories.Where(c => c.Id == p.CategoryId).Select(c => new CategoryRef(c.Name, c.Slug)).FirstOrDefault(),
                    Variants = p.Variants.Where(v => v.IsActive)
                        .OrderBy(v => v.Price)
                        .Select(v => new
                        {
                            v.Id,
                            v.Sku,
                            v.Name,
                            v.Price,
                            v.CompareAtPrice,
                            v.SoldByWeight,
                            ValueIds = v.AttributeValues.Select(a => a.AttributeValueId).ToList(),
                        })
                        .ToList(),
                })
                .SingleOrDefaultAsync(cancellationToken);

            if (product is null)
            {
                return Error.NotFound("catalog.product_not_found", "No published product has this address.");
            }

            var valueIds = product.Variants.SelectMany(v => v.ValueIds).Distinct().ToList();
            var values = await (
                    from value in db.AttributeValues.AsNoTracking()
                    join attribute in db.Attributes on value.AttributeId equals attribute.Id
                    where valueIds.Contains(value.Id)
                    orderby attribute.NormalizedName, value.Position, value.Id
                    select new { value.Id, AttributeId = attribute.Id, Attribute = attribute.Name, value.Value })
                .ToListAsync(cancellationToken);

            var options = values
                .GroupBy(v => (v.AttributeId, v.Attribute))
                .Select(group => new OptionItem(group.Key.Attribute, group.Select(v => v.Value).ToList()))
                .ToList();

            // A value's index in the ordered list puts each variant's attributes in the same order as the options.
            var ordered = values.Select((value, index) => (value, index)).ToDictionary(entry => entry.value.Id);
            var variants = product.Variants
                .Select(v => new VariantItem(
                    v.Id,
                    v.Sku,
                    v.Name,
                    v.Price,
                    v.CompareAtPrice,
                    v.SoldByWeight,
                    v.ValueIds.Select(id => ordered[id])
                        .OrderBy(entry => entry.index)
                        .Select(entry => new VariantAttribute(entry.value.Attribute, entry.value.Value))
                        .ToList()))
                .ToList();

            return new Response(product.Id, product.Name, product.Slug, product.Description, product.Brand, product.Category, options, variants);
        }
    }
}
