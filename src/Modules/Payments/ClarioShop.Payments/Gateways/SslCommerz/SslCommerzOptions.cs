namespace ClarioShop.Payments.Gateways.SslCommerz;

/// <summary>SSLCommerz endpoints, section "Payments:SslCommerz". Merchant credentials are per store, in Settings.</summary>
internal sealed class SslCommerzOptions
{
    public const string SectionName = "Payments:SslCommerz";

    public Uri SandboxBaseUrl { get; set; } = new("https://sandbox.sslcommerz.com/");

    public Uri LiveBaseUrl { get; set; } = new("https://securepay.sslcommerz.com/");

    /// <summary>
    /// The storefront page, on the store's own host, the shopper's browser is sent to after the payment page, with
    /// <c>orderId</c> and <c>payment</c> (<c>paid</c>, <c>pending</c>, <c>failed</c> or <c>cancelled</c>) in the query.
    /// </summary>
    public string ShopperReturnPath { get; set; } = "/checkout/payment";
}
