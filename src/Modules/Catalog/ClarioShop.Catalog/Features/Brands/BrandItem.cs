using ClarioShop.Catalog.Domain;

namespace ClarioShop.Catalog.Features.Brands;

public sealed record BrandItem(Guid Id, string Name, string Slug)
{
    internal static BrandItem From(Brand brand) => new(brand.Id, brand.Name, brand.Slug);
}
