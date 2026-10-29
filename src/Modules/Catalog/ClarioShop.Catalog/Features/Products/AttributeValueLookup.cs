using ClarioShop.Catalog.Domain;
using ClarioShop.Catalog.Persistence;

namespace ClarioShop.Catalog.Features.Products;

/// <summary>The attribute values a request names, each with the attribute it belongs to.</summary>
internal sealed class AttributeValueLookup(IReadOnlyDictionary<Guid, AttributeValueRef> values)
{
    /// <summary>Loads the values; an id that is not a value of any attribute is a validation error, like an unknown category.</summary>
    public static async Task<Result<AttributeValueLookup>> LoadAsync(
        CatalogDbContext db, IEnumerable<Guid> valueIds, CancellationToken cancellationToken)
    {
        var ids = valueIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new AttributeValueLookup(new Dictionary<Guid, AttributeValueRef>());
        }

        var found = await db.AttributeValues.AsNoTracking()
            .Where(value => ids.Contains(value.Id))
            .ToDictionaryAsync(value => value.Id, value => new AttributeValueRef(value.AttributeId, value.Id), cancellationToken);

        return found.Count == ids.Count
            ? new AttributeValueLookup(found)
            : Error.Validation("attributeValueIds", "An attribute value does not exist.");
    }

    public IReadOnlyList<AttributeValueRef> For(IReadOnlyList<Guid>? valueIds) =>
        valueIds?.Select(id => values[id]).ToList() ?? [];
}
