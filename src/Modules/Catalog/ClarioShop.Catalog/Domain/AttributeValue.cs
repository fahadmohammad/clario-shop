using ClarioShop.SharedKernel.Domain;

namespace ClarioShop.Catalog.Domain;

/// <summary>One choice of a <see cref="ProductAttribute"/>, e.g. "XL" for Size.</summary>
internal sealed class AttributeValue : Entity
{
    private AttributeValue()
    {
    }

    public Guid AttributeId { get; private set; }

    public string Value { get; private set; } = string.Empty;

    /// <summary>Upper-cased value, unique within its attribute.</summary>
    public string NormalizedValue { get; private set; } = string.Empty;

    /// <summary>Where the value sits among its attribute's values, so sizes read S, M, L rather than alphabetically.</summary>
    public int Position { get; private set; }

    internal static AttributeValue Create(Guid attributeId, string value, int position) => new()
    {
        Id = Guid.CreateVersion7(),
        AttributeId = attributeId,
        Value = value.Trim(),
        NormalizedValue = ProductAttribute.Normalize(value),
        Position = position,
    };

    internal void Rename(string value)
    {
        Value = value.Trim();
        NormalizedValue = ProductAttribute.Normalize(value);
    }
}
