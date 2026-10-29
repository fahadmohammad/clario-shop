using ClarioShop.Notifications.Domain;

namespace ClarioShop.Notifications.Tests.Domain;

public sealed class NotificationLogTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    private static readonly RetryPolicy Policy = new(MaxAttempts: 3, FirstDelay: TimeSpan.FromMinutes(1), MaxDelay: TimeSpan.FromHours(1), ClaimTimeout: TimeSpan.FromMinutes(5));

    [Fact]
    public void Create_Sms_IsPendingAndDueNowWithoutSubject()
    {
        var log = Sms();

        Assert.Equal((NotificationStatus.Pending, 0, (DateTimeOffset?)Now, (string?)null), (log.Status, log.Attempts, log.NextAttemptAt, log.Subject));
    }

    [Fact]
    public void Create_LongSubjectAndBody_AreClipped()
    {
        var log = NotificationLog.Create(Guid.CreateVersion7(), NotificationKind.OrderPlaced, Guid.CreateVersion7(), "CS-000001", Guid.CreateVersion7(),
            NotificationChannel.Email, "rahim@example.com", "en", new string('s', 300), new string('b', 3000), Now);

        Assert.Equal((NotificationLog.MaxSubjectLength, NotificationLog.MaxBodyLength), (log.Subject!.Length, log.Body.Length));
    }

    [Fact]
    public void TryClaim_Due_StartsATryAndHoldsItForTheClaimTimeout()
    {
        var log = Sms();

        var claimed = log.TryClaim(Policy, Now);

        Assert.True(claimed);
        Assert.Equal((1, (DateTimeOffset?)Now.AddMinutes(5)), (log.Attempts, log.NextAttemptAt));
    }

    [Fact]
    public void TryClaim_AlreadyClaimed_IsRefusedUntilTheClaimTimesOut()
    {
        var log = Sms();
        log.TryClaim(Policy, Now);

        var during = log.TryClaim(Policy, Now.AddMinutes(4));
        var after = log.TryClaim(Policy, Now.AddMinutes(5));

        Assert.Equal((false, true, 2), (during, after, log.Attempts));
    }

    [Fact]
    public void TryClaim_Sent_IsRefused()
    {
        var log = Sms();
        log.TryClaim(Policy, Now);
        log.MarkSent(Now);

        var claimed = log.TryClaim(Policy, Now.AddDays(1));

        Assert.False(claimed);
        Assert.Equal((NotificationStatus.Sent, (DateTimeOffset?)Now, (DateTimeOffset?)null), (log.Status, log.SentAt, log.NextAttemptAt));
    }

    [Fact]
    public void RecordFailure_TriesLeft_WaitsLongerAfterEachTry()
    {
        var log = Sms();

        log.TryClaim(Policy, Now);
        log.RecordFailure("Timeout", Policy, Now);
        var firstRetry = log.NextAttemptAt;
        log.TryClaim(Policy, Now.AddMinutes(1));
        log.RecordFailure("Timeout again", Policy, Now.AddMinutes(1));

        Assert.Equal(Now.AddMinutes(1), firstRetry);
        Assert.Equal((NotificationStatus.Pending, (DateTimeOffset?)Now.AddMinutes(3), "Timeout again"), (log.Status, log.NextAttemptAt, log.LastError));
    }

    [Fact]
    public void RecordFailure_LastTry_FailsForGood()
    {
        var log = Sms();

        for (var attempt = 0; attempt < Policy.MaxAttempts; attempt++)
        {
            log.TryClaim(Policy, Now.AddHours(attempt));
            log.RecordFailure("Gateway down", Policy, Now.AddHours(attempt));
        }

        Assert.Equal((NotificationStatus.Failed, 3, (DateTimeOffset?)null), (log.Status, log.Attempts, log.NextAttemptAt));
        Assert.False(log.TryClaim(Policy, Now.AddDays(1)));
    }

    [Fact]
    public void MarkSent_AfterAFailedTry_ClearsTheError()
    {
        var log = Sms();
        log.TryClaim(Policy, Now);
        log.RecordFailure("Timeout", Policy, Now);
        log.TryClaim(Policy, Now.AddMinutes(1));

        log.MarkSent(Now.AddMinutes(1));

        Assert.Equal((NotificationStatus.Sent, 2, (string?)null), (log.Status, log.Attempts, log.LastError));
    }

    [Fact]
    public void RecordFailure_LongError_IsClipped()
    {
        var log = Sms();
        log.TryClaim(Policy, Now);

        log.RecordFailure(new string('e', 1000), Policy, Now);

        Assert.Equal(NotificationLog.MaxErrorLength, log.LastError!.Length);
    }

    private static NotificationLog Sms() =>
        NotificationLog.Create(Guid.CreateVersion7(), NotificationKind.OrderShipped, Guid.CreateVersion7(), "CS-000001", Guid.CreateVersion7(),
            NotificationChannel.Sms, "+8801712345678", "en", "ignored for SMS", "Your order CS-000001 is on its way.", Now);
}
