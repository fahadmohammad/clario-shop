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

    public static readonly Error DeliveryZoneRequired =
        Error.Validation("deliveryZoneId", "Choose a delivery zone.");

    public static readonly Error UnknownDeliveryZone =
        Error.Validation("deliveryZoneId", "The store does not deliver to this zone; choose one of its delivery zones.");

    public static readonly Error PhoneRequired =
        Error.Validation("phone", "Enter your mobile number, or sign in.");

    public static readonly Error NameRequired =
        Error.Validation("name", "Enter the name to deliver to.");

    public static readonly Error NotShoppersPhone =
        Error.Validation("phone", "You are signed in, so the order uses your account's mobile number; leave the phone out or send that number.");

    public static readonly Error SavedAddressNeedsSignIn =
        Error.Validation("savedAddressId", "Sign in to deliver to a saved address.");

    public static readonly Error SavedAddressNotFound =
        Error.Validation("savedAddressId", "This is not one of your saved addresses.");

    public static readonly Error ShopperNotFound =
        Error.NotFound("ordering.shopper_not_found", "Your customer account no longer exists; sign in again.");

    public static readonly Error OrderNotFound =
        Error.NotFound("ordering.order_not_found", "The order does not exist.");

    public static readonly Error NotForSale =
        Error.Validation("variantId", "This item is not for sale.");

    public static readonly Error WholeUnitsOnly =
        Error.Validation("quantity", "This item is sold in whole units.");

    public static readonly Error OrderChanged =
        Error.Conflict("ordering.order_changed", "The order was changed by someone else; reload it and try again.");

    public static readonly Error AwaitingPayment =
        Error.Conflict("ordering.awaiting_payment", "The order is paid online and is confirmed once its payment arrives.");

    public static Error PaymentMethodUnavailable(string method) =>
        Error.Conflict("ordering.payment_method_unavailable", $"The store does not take {method} payments at the moment; choose another way to pay.");

    public static readonly Error PartlyPaid =
        Error.Conflict("ordering.partly_paid", "Some of the order is paid, so it is not cancelled automatically; staff refund or settle it.");

    public static Error InvalidTransition(OrderStatus from, OrderStatus to) =>
        Error.Conflict("ordering.invalid_transition", $"A {from} order cannot be moved to {to}.");

    public static Error StockGone(string detail) =>
        Error.Conflict(
            "ordering.stock_unavailable",
            $"The order's stock hold expired and the stock is no longer available, so it cannot be confirmed ({detail}). Cancel the order or restock first.");

    public static Error ItemsUnavailable(IEnumerable<string> skus) =>
        Error.Conflict(
            "ordering.items_unavailable",
            $"Some items in the cart are no longer for sale ({string.Join(", ", skus)}); remove them and try again.");
}
