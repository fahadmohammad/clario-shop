using ClarioShop.SharedKernel.Domain;
using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Catalog.Domain;

/// <summary>
/// A way variants of one product differ, such as Size or Colour, with the values staff pick from (S, M, L).
/// Named ProductAttribute so it never shadows <see cref="System.Attribute"/>.
/// </summary>
internal sealed class ProductAttribute : Entity
{
    private readonly List<AttributeValue> values = [];

    private ProductAttribute()
    {
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Upper-cased name, unique per shop, so "Size" and "SIZE" cannot become two attributes.</summary>
    public string NormalizedName { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>In the order staff added them, which is the order the storefront offers them (S, M, L, XL).</summary>
    public IReadOnlyCollection<AttributeValue> Values => values;

    public static Result<ProductAttribute> Create(string name, IReadOnlyList<string> values, DateTimeOffset now)
    {
        var duplicate = values.GroupBy(Normalize).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            return Error.Validation("values", $"{duplicate.First().Trim()} is given twice.");
        }

        var attribute = new ProductAttribute
        {
            Id = Guid.CreateVersion7(),
            Name = name.Trim(),
            NormalizedName = Normalize(name),
            CreatedAt = now,
        };

        foreach (var value in values)
        {
            attribute.values.Add(AttributeValue.Create(attribute.Id, value, attribute.values.Count));
        }

        return attribute;
    }

    public static string Normalize(string text) => text.Trim().ToUpperInvariant();

    public void Rename(string name)
    {
        Name = name.Trim();
        NormalizedName = Normalize(name);
    }

    /// <summary>Adds a value after the existing ones.</summary>
    public Result<AttributeValue> AddValue(string value)
    {
        if (ValueTaken(value, exceptValueId: null))
        {
            return CatalogErrors.AttributeValueTaken(Name, value.Trim());
        }

        var added = AttributeValue.Create(Id, value, values.Count == 0 ? 0 : values.Max(v => v.Position) + 1);
        values.Add(added);
        return added;
    }

    /// <summary>Renames a value; variants keep pointing at it, so the storefront shows the new spelling.</summary>
    public Result RenameValue(Guid valueId, string value)
    {
        var existing = values.FirstOrDefault(v => v.Id == valueId);
        if (existing is null)
        {
            return CatalogErrors.AttributeValueNotFound;
        }

        if (ValueTaken(value, valueId))
        {
            return CatalogErrors.AttributeValueTaken(Name, value.Trim());
        }

        existing.Rename(value);
        return Result.Success();
    }

    private bool ValueTaken(string value, Guid? exceptValueId)
    {
        var normalized = Normalize(value);
        return values.Any(v => v.NormalizedValue == normalized && v.Id != exceptValueId);
    }
}
