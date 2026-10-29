using ClarioShop.Notifications.Domain;

namespace ClarioShop.Notifications.Tests.Domain;

public sealed class StaffAlertLogTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateStaffAlert_Email_IsPendingWithoutOrder()
    {
        var alertId = Guid.CreateVersion7();

        var log = NotificationLog.CreateStaffAlert(alertId, NotificationKind.StockWentNegative, NotificationChannel.Email,
            "owner@shop.test", "en", "Stock below zero", "RICE-5KG is below zero.", Now);

        Assert.Equal(
            (alertId, NotificationStatus.Pending, (DateTimeOffset?)Now, (Guid?)null, (string?)null, (Guid?)null, "Stock below zero"),
            (log.EventId, log.Status, log.NextAttemptAt, log.OrderId, log.OrderNumber, log.CustomerId, log.Subject));
    }

    [Fact]
    public void CreateStaffAlert_Sms_HasNoSubject()
    {
        var log = NotificationLog.CreateStaffAlert(Guid.CreateVersion7(), NotificationKind.StockDriftDetected, NotificationChannel.Sms,
            "+8801712345678", "bn", "ignored for SMS", "স্টক", Now);

        Assert.Null(log.Subject);
    }
}
