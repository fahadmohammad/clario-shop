using ClarioShop.Notifications.Domain;

namespace ClarioShop.Notifications.Tests.Domain;

public sealed class RetryPolicyTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(6, 32)]
    [InlineData(7, 60)]
    [InlineData(50, 60)]
    public void DelayAfter_FailedTries_DoublesUpToTheMaximum(int failedAttempts, int expectedMinutes)
    {
        var policy = new RetryPolicy(MaxAttempts: 100, FirstDelay: TimeSpan.FromMinutes(1), MaxDelay: TimeSpan.FromHours(1), ClaimTimeout: TimeSpan.FromMinutes(5));

        var delay = policy.DelayAfter(failedAttempts);

        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), delay);
    }
}
