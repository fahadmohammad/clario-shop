using ClarioShop.Catalog.Contracts;
using ClarioShop.Ordering.Domain;

namespace ClarioShop.Ordering.Features.Carts;

/// <summary>A cart priced with the catalog's current prices. Items no longer for sale are flagged and left out of the subtotal.</summary>
public sealed record CartDetails(Guid Id, IReadOnlyList<CartLine> Items, decimal Subtotal, Guid? OrderId)
{
    internal static async Task<CartDetails> PriceAsync(Cart cart, ICatalogQueries catalog, CancellationToken cancellationToken)
    {
        var variants = cart.Items.Count == 0
            ? []
            : (await catalog.GetVariantsAsync([.. cart.Items.Select(item => item.VariantId)], cancellationToken)).ToDictionary(v => v.VariantId);

        var lines = cart.Items
            .OrderBy(item => item.AddedAt)
            .ThenBy(item => item.Id)
            .Select(item => CartLine.From(item, variants.GetValueOrDefault(item.VariantId)))
            .ToList();

        return new CartDetails(cart.Id, lines, lines.Where(line => line.IsAvailable).Sum(line => line.LineTotal ?? 0), cart.OrderId);
    }
}

/// <summary>Name, SKU and prices are null when the variant no longer exists in the catalog.</summary>
public sealed record CartLine(
    Guid VariantId,
    Guid? ProductId,
    string? ProductName,
    string? VariantName,
    string? Sku,
    decimal? UnitPrice,
    decimal Quantity,
    decimal? LineTotal,
    bool SoldByWeight,
    bool IsAvailable)
{
    internal static CartLine From(CartItem item, VariantSummary? variant) => variant is null
        ? new(item.VariantId, null, null, null, null, null, item.Quantity, null, false, false)
        : new(
            item.VariantId,
            variant.ProductId,
            variant.ProductName,
            variant.Name,
            variant.Sku,
            variant.Price,
            item.Quantity,
            Money.Round(variant.Price * item.Quantity),
            variant.SoldByWeight,
            variant.IsSellable);
}
