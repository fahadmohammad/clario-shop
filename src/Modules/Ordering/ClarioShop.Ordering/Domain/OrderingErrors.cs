using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Ordering.Domain;

internal static class OrderingErrors
{
    public static readonly Error CartNotFound =
        Error.NotFound("ordering.cart_not_found", "The cart does not exist.");

    public static readonly Error CartItemNotFound =
        Error.NotFound("ordering.cart_item_not_found", "The item is not in the cart.");

    public static readonly Error CartCheckedOut =
        Error.Conflict("ordering.cart_checked_out", "The cart was already checked out; start a new cart.");

    public static readonly Error CartChanged =
        Error.Conflict("ordering.cart_changed", "The cart changed while saving; please try again.");

    public static readonly Error CartEmpty =
        Error.Validation("cartId", "The cart is empty.");

    public static readonly Error TooManyCartItems =
        Error.Validation("variantId", $"A cart can hold at most {Cart.MaxItems} different items.");

    public static readonly Error QuantityTooLarge =
        Error.Validation("quantity", $"At most {Cart.MaxQuantity:0} of one item per order.");

    public static readonly Error OrderNotFound =
        Error.NotFound("ordering.order_not_found", "The order does not exist.");

    public static readonly Error NotForSale =
        Error.Validation("variantId", "This item is not for sale.");

    public static readonly Error WholeUnitsOnly =
        Error.Validation("quantity", "This item is sold in whole units.");

    public static Error ItemsUnavailable(IEnumerable<string> skus) =>
        Error.Conflict(
            "ordering.items_unavailable",
            $"Some items in the cart are no longer for sale ({string.Join(", ", skus)}); remove them and try again.");
}
