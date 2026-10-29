namespace ClarioShop.Catalog.Domain;

/// <summary>
/// The value a variant has for one attribute. Keyed by variant and attribute, so a variant has at most one value
/// per attribute.
/// </summary>
internal sealed class VariantAttributeValue
{
    private VariantAttributeValue()
    {
    }

    public Guid AttributeId { get; private set; }

    public Guid AttributeValueId { get; private set; }

    internal static VariantAttributeValue Create(AttributeValueRef value) => new()
    {
        AttributeId = value.AttributeId,
        AttributeValueId = value.ValueId,
    };

    internal void Change(Guid valueId) => AttributeValueId = valueId;
}

/// <summary>An attribute value as a variant refers to it: the value and the attribute it belongs to.</summary>
internal sealed record AttributeValueRef(Guid AttributeId, Guid ValueId);
