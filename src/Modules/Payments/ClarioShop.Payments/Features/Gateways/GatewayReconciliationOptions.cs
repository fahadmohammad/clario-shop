namespace ClarioShop.Payments.Features.Gateways;

/// <summary>The reconciliation job for online payments, section "Payments:Reconciliation".</summary>
internal sealed class GatewayReconciliationOptions
{
    public const string SectionName = "Payments:Reconciliation";

    /// <summary>How often every store's pending online payments are checked with the gateway.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// How long after the shopper's last try a payment the gateway has no paid try for stays open, so they can retry
    /// on the payment page or start again; after that it fails and the order is cancelled.
    /// </summary>
    public TimeSpan PaymentWindow { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Payments pending longer than this are no longer checked (e.g. one the gateway holds for review, or keeps as
    /// still processing); they are left to staff.
    /// </summary>
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromDays(2);

    /// <summary>The most payments checked per store in one run; the rest wait for the next run.</summary>
    public int BatchSize { get; set; } = 200;
}
