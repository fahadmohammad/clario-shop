namespace ClarioShop.Payments.Gateways.SslCommerz;

/// <summary>SSLCommerz endpoints, section "Payments:SslCommerz". Merchant credentials are per store, in Settings.</summary>
internal sealed class SslCommerzOptions
{
    public const string SectionName = "Payments:SslCommerz";

    public Uri SandboxBaseUrl { get; set; } = new("https://sandbox.sslcommerz.com/");

    public Uri LiveBaseUrl { get; set; } = new("https://securepay.sslcommerz.com/");
}
