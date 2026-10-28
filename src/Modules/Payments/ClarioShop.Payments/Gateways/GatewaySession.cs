namespace ClarioShop.Payments.Gateways;

/// <param name="SessionKey">The gateway's id for the session (SSLCommerz <c>sessionkey</c>).</param>
/// <param name="RedirectUrl">The gateway's payment page for the shopper.</param>
internal sealed record GatewaySession(string SessionKey, Uri RedirectUrl);
