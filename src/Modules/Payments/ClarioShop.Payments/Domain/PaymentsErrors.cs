using System.Globalization;
using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Payments.Domain;

internal static class PaymentsErrors
{
    public static readonly Error OrderNotFound =
        Error.NotFound("payments.order_not_found", "The order does not exist.");

    public static Error GatewayNotConfigured(string gateway) =>
        Error.Conflict("payments.gateway_not_configured", $"The store does not take payments through {gateway}: its merchant account is not set up or is switched off.");

    public static Error GatewayRefused(string gateway) =>
        Error.Unavailable("payments.gateway_refused", $"{gateway} refused the request; check the store's merchant account, or try again later.");

    public static Error GatewayUnavailable(string gateway) =>
        Error.Unavailable("payments.gateway_unavailable", $"{gateway} did not answer; try again in a moment.");

    public static Error NotOutForDelivery(string status) =>
        Error.Conflict("payments.not_out_for_delivery", $"Cash is collected on delivery, but the order is {status}; ship it first.");

    public static Error ExceedsOutstanding(decimal amount, decimal outstanding) =>
        Error.Conflict(
            "payments.exceeds_outstanding",
            outstanding == 0m
                ? "The order is already paid in full."
                : string.Create(CultureInfo.InvariantCulture, $"{amount:0.00} is more than the {outstanding:0.00} still owed on the order."));
}
