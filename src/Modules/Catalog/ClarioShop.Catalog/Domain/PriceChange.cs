namespace ClarioShop.Catalog.Domain;

/// <summary>A variant's price before and after a change.</summary>
internal sealed record PriceChange(Variant Variant, decimal OldPrice, decimal? OldCompareAtPrice)
{
    public bool IsChange => OldPrice != Variant.Price || OldCompareAtPrice != Variant.CompareAtPrice;
}
