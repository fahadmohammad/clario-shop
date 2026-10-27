using ClarioShop.SharedKernel.Domain;

namespace ClarioShop.Catalog.Domain;

/// <summary>The sellable unit: one SKU, one price. Orders, POS sales and stock all point at a variant.</summary>
internal sealed class Variant : Entity
{
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

    internal static Variant Create(Guid productId, VariantSpec spec) => new()
    {
        Id = Guid.CreateVersion7(),
        ProductId = productId,
        Sku = spec.Sku.Trim().ToUpperInvariant(),
        Name = spec.Name?.Trim() ?? string.Empty,
        Price = decimal.Round(spec.Price, 2),
        CompareAtPrice = spec.CompareAtPrice is { } compare ? decimal.Round(compare, 2) : null,
        Barcodes = spec.Barcodes?.Select(code => code.Trim()).Where(code => code.Length > 0).Distinct().ToList() ?? [],
        SoldByWeight = spec.SoldByWeight,
        IsActive = true,
    };
}

internal sealed record VariantSpec(
    string Sku,
    string? Name,
    decimal Price,
    decimal? CompareAtPrice,
    IReadOnlyList<string>? Barcodes,
    bool SoldByWeight);
