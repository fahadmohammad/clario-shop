using ClarioShop.SharedKernel.Domain;

namespace ClarioShop.Catalog.Domain;

/// <summary>The sellable unit: one SKU, one price. Orders, POS sales and stock all point at a variant.</summary>
internal sealed class Variant : Entity
{
    private readonly List<VariantAttributeValue> attributeValues = [];

    private Variant()
    {
    }

    public Guid ProductId { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    /// <summary>What distinguishes this variant, e.g. "1 kg" or "Red / XL". Empty for single-variant products.</summary>
    public string Name { get; private set; } = string.Empty;

    public decimal Price { get; private set; }

    public decimal? CompareAtPrice { get; private set; }

    /// <summary>Manufacturer and in-house barcodes scanned at the POS.</summary>
    public List<string> Barcodes { get; private set; } = [];

    /// <summary>Loose goods priced per kg and read from weighing-scale labels.</summary>
    public bool SoldByWeight { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>Concurrency token (PostgreSQL xmin), so two price changes cannot both start from the same price.</summary>
    public uint RowVersion { get; private set; }

    /// <summary>The variant's value for each attribute it has, e.g. Size: M and Colour: Red.</summary>
    public IReadOnlyCollection<VariantAttributeValue> AttributeValues => attributeValues;

    internal static string NormalizeSku(string sku) => sku.Trim().ToUpperInvariant();

    internal static Variant Create(Guid productId, VariantSpec spec)
    {
        var variant = new Variant
        {
            Id = Guid.CreateVersion7(),
            ProductId = productId,
            Sku = NormalizeSku(spec.Sku),
            Name = spec.Name?.Trim() ?? string.Empty,
            Price = decimal.Round(spec.Price, 2),
            CompareAtPrice = spec.CompareAtPrice is { } compare ? decimal.Round(compare, 2) : null,
            Barcodes = NormalizeBarcodes(spec.Barcodes),
            SoldByWeight = spec.SoldByWeight,
            IsActive = true,
        };
        variant.attributeValues.AddRange((spec.AttributeValues ?? []).Select(VariantAttributeValue.Create));
        return variant;
    }

    /// <summary>Value ids sorted, so two variants with the same values compare equal; empty without attributes.</summary>
    internal static string CombinationKey(IEnumerable<Guid> valueIds) => string.Join(',', valueIds.Order());

    internal string CombinationKey() => CombinationKey(attributeValues.Select(value => value.AttributeValueId));

    internal void Update(string? name, IReadOnlyList<string>? barcodes, bool isActive, IReadOnlyList<AttributeValueRef> values)
    {
        Name = name?.Trim() ?? string.Empty;
        Barcodes = NormalizeBarcodes(barcodes);
        IsActive = isActive;
        SetAttributeValues(values);
    }

    internal void SetPrice(decimal price, decimal? compareAtPrice)
    {
        Price = decimal.Round(price, 2);
        CompareAtPrice = compareAtPrice is { } compare ? decimal.Round(compare, 2) : null;
    }

    /// <summary>
    /// Changes the rows in place rather than replacing them, so EF Core updates a changed value instead of deleting
    /// and re-adding a row with the same key.
    /// </summary>
    private void SetAttributeValues(IReadOnlyList<AttributeValueRef> values)
    {
        attributeValues.RemoveAll(current => values.All(value => value.AttributeId != current.AttributeId));
        foreach (var value in values)
        {
            var current = attributeValues.FirstOrDefault(existing => existing.AttributeId == value.AttributeId);
            if (current is null)
            {
                attributeValues.Add(VariantAttributeValue.Create(value));
            }
            else
            {
                current.Change(value.ValueId);
            }
        }
    }

    private static List<string> NormalizeBarcodes(IReadOnlyList<string>? barcodes) =>
        barcodes?.Select(code => code.Trim()).Where(code => code.Length > 0).Distinct().ToList() ?? [];
}

internal sealed record VariantSpec(
    string Sku,
    string? Name,
    decimal Price,
    decimal? CompareAtPrice,
    IReadOnlyList<string>? Barcodes,
    bool SoldByWeight,
    IReadOnlyList<AttributeValueRef>? AttributeValues = null);
