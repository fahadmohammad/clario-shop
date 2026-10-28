using ClarioShop.Settings.Domain;

namespace ClarioShop.Settings.Features.PaymentGateways;

/// <summary>A payment gateway account as staff see it: never the secret itself.</summary>
/// <param name="Gateway">E.g. <c>SslCommerz</c>.</param>
/// <param name="MerchantId">The merchant's id with the gateway, e.g. the SSLCommerz store id.</param>
public sealed record PaymentGatewayResponse(string Gateway, string MerchantId, bool UseSandbox, bool IsEnabled, DateTimeOffset UpdatedAt)
{
    internal static PaymentGatewayResponse From(PaymentGatewayAccount account) =>
        new(account.Gateway.ToString(), account.MerchantId, account.UseSandbox, account.IsEnabled, account.UpdatedAt);
}
