using ClarioShop.Payments.Gateways;

namespace ClarioShop.Payments.Features.Gateways;

/// <summary>Where a gateway calls back, on the store's own host so the tenant is resolved as for any request.</summary>
internal static class GatewayCallbackRoutes
{
    public const string Ipn = "ipn";

    public const string Success = "success";

    public const string Fail = "fail";

    public const string Cancel = "cancel";

    /// <summary>E.g. <c>/api/payments/sslcommerz</c>.</summary>
    public static string BasePath(string gateway) => $"/api/payments/{gateway.ToLowerInvariant()}";

    public static GatewayCallbacks For(string gateway, Uri callbackBase) => new(
        new Uri(callbackBase, $"{BasePath(gateway)}/{Success}"),
        new Uri(callbackBase, $"{BasePath(gateway)}/{Fail}"),
        new Uri(callbackBase, $"{BasePath(gateway)}/{Cancel}"),
        new Uri(callbackBase, $"{BasePath(gateway)}/{Ipn}"));
}
