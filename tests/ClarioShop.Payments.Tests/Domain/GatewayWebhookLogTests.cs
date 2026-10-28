using ClarioShop.Payments.Domain;

namespace ClarioShop.Payments.Tests.Domain;

public sealed class GatewayWebhookLogTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Receive_StoresPayloadAndTrimmedIdsAsReceived()
    {
        var log = GatewayWebhookLog.Receive("SslCommerz", GatewayCallbackSource.Ipn, """{"tran_id":"T"}""", " T ", "", "VALID", Now);

        Assert.Equal(("T", (string?)null, "VALID", GatewayCallbackOutcome.Received), (log.TransactionId, log.ValidationId, log.ReportedStatus, log.Outcome));
        Assert.Equal("""{"tran_id":"T"}""", log.Payload);
    }

    [Fact]
    public void Receive_OverlongIds_AreClipped()
    {
        var log = GatewayWebhookLog.Receive("SslCommerz", GatewayCallbackSource.Ipn, "{}", new string('x', 500), null, null, Now);

        Assert.Equal(GatewayWebhookLog.MaxIdLength, log.TransactionId!.Length);
    }

    [Fact]
    public void Complete_RecordsOutcome()
    {
        var log = GatewayWebhookLog.Receive("SslCommerz", GatewayCallbackSource.SuccessRedirect, "{}", "T", "V", "VALID", Now);
        var paymentId = Guid.CreateVersion7();

        log.Complete(GatewayCallbackOutcome.Rejected, paymentId, "payments.amount_mismatch", Now.AddSeconds(1));

        Assert.Equal((GatewayCallbackOutcome.Rejected, (Guid?)paymentId, "payments.amount_mismatch", (DateTimeOffset?)Now.AddSeconds(1)), (log.Outcome, log.PaymentId, log.Detail, log.ProcessedAt));
    }
}
