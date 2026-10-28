using System.Globalization;
using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Payments.Domain;

internal static class PaymentsErrors
{
    public static readonly Error OrderNotFound =
        Error.NotFound("payments.order_not_found", "The order does not exist.");

    public static Error NotOutForDelivery(string status) =>
        Error.Conflict("payments.not_out_for_delivery", $"Cash is collected on delivery, but the order is {status}; ship it first.");

    public static Error ExceedsOutstanding(decimal amount, decimal outstanding) =>
        Error.Conflict(
            "payments.exceeds_outstanding",
            outstanding == 0m
                ? "The order is already paid in full."
                : string.Create(CultureInfo.InvariantCulture, $"{amount:0.00} is more than the {outstanding:0.00} still owed on the order."));
}
