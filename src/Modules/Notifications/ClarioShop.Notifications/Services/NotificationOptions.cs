using ClarioShop.Notifications.Domain;

namespace ClarioShop.Notifications.Services;

/// <summary>Sending and retrying shopper notifications, section "Notifications".</summary>
internal sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    /// <summary>How often every store's messages that are due for a retry are tried again.</summary>
    public TimeSpan RetryInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Tries in all, the first one included, before a message is given up as failed.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>The wait after the first failed try; it doubles after each further one, up to <see cref="MaxRetryDelay"/>.</summary>
    public TimeSpan FirstRetryDelay { get; set; } = TimeSpan.FromMinutes(1);

    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromHours(1);

    /// <summary>How long one try may take before another sender may take the message over.</summary>
    public TimeSpan ClaimTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>The most messages retried per store in one run; the rest wait for the next run.</summary>
    public int BatchSize { get; set; } = 100;

    public RetryPolicy RetryPolicy => new(MaxAttempts, FirstRetryDelay, MaxRetryDelay, ClaimTimeout);
}
