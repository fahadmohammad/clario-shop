using ClarioShop.Catalog.Contracts;
using ClarioShop.Ordering.Domain;

namespace ClarioShop.Ordering.Features.Carts;

internal static class CartItemRules
{
    /// <summary>Only sellable variants go into a cart, and only items sold by weight take fractional quantities.</summary>
    public static async Task<Result> CheckAsync(ICatalogQueries catalog, Guid variantId, decimal quantity, CancellationToken cancellationToken)
    {
        var variant = (await catalog.GetVariantsAsync([variantId], cancellationToken)).SingleOrDefault();
        if (variant is not { IsSellable: true })
        {
            return OrderingErrors.NotForSale;
        }

        return !variant.SoldByWeight && quantity != decimal.Truncate(quantity)
            ? OrderingErrors.WholeUnitsOnly
            : Result.Success();
    }

    public static IRuleBuilderOptions<T, decimal> ValidCartQuantity<T>(this IRuleBuilder<T, decimal> rule) =>
        rule.GreaterThan(0).LessThanOrEqualTo(Cart.MaxQuantity).PrecisionScale(18, 3, ignoreTrailingZeros: true);
}
