using ClarioShop.Catalog.Domain;

namespace ClarioShop.Catalog.Features.Attributes;

public sealed record AttributeItem(Guid Id, string Name, IReadOnlyList<AttributeValueItem> Values)
{
    internal static AttributeItem From(ProductAttribute attribute) => new(
        attribute.Id,
        attribute.Name,
        attribute.Values.OrderBy(v => v.Position).ThenBy(v => v.Id).Select(v => new AttributeValueItem(v.Id, v.Value)).ToList());
}

public sealed record AttributeValueItem(Guid Id, string Value);
