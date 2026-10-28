namespace ClarioShop.Notifications.Domain;

/// <summary>How often a message that could not be sent is tried again.</summary>
/// <param name="MaxAttempts">Tries in all, the first one included; after the last it is <see cref="NotificationStatus.Failed"/>.</param>
/// <param name="FirstDelay">The wait after the first failed try; it doubles after each further one.</param>
/// <param name="MaxDelay">The longest wait between two tries.</param>
/// <param name="ClaimTimeout">
/// How long a try may take before another sender may take the message over (e.g. after the process died mid-send).
/// </param>
internal sealed record RetryPolicy(int MaxAttempts, TimeSpan FirstDelay, TimeSpan MaxDelay, TimeSpan ClaimTimeout)
{
    /// <summary>The wait after <paramref name="failedAttempts"/> failed tries (1 or more).</summary>
    public TimeSpan DelayAfter(int failedAttempts)
    {
        var delay = FirstDelay;
        for (var attempt = 1; attempt < failedAttempts && delay < MaxDelay; attempt++)
        {
            delay *= 2;
        }

        return delay < MaxDelay ? delay : MaxDelay;
    }
}
