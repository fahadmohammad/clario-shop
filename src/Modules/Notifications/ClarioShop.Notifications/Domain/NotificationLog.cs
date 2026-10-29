using ClarioShop.SharedKernel.Domain;

namespace ClarioShop.Notifications.Domain;

/// <summary>
/// One message to one recipient, for audit and retries: what was sent, to whom, and how each try went. There is at
/// most one per event and channel, so a redelivered event never sends the same message twice. A message to a shopper
/// is about an order; a stock alert goes to the store's contact and has no order.
/// </summary>
internal sealed class NotificationLog : Entity
{
    public const int MaxRecipientLength = 254;

    public const int MaxSubjectLength = 200;

    public const int MaxBodyLength = 2000;

    public const int MaxErrorLength = 500;

    private NotificationLog()
    {
    }

    /// <summary>
    /// What the message is sent once for: the integration event that caused it, or for a drift alert the drift check
    /// run, so a check that corrects many stock levels sends one alert.
    /// </summary>
    public Guid EventId { get; private set; }

    public NotificationKind Kind { get; private set; }

    /// <summary>Null for a stock alert.</summary>
    public Guid? OrderId { get; private set; }

    /// <summary>E.g. <c>CS-000123</c>; null for a stock alert.</summary>
    public string? OrderNumber { get; private set; }

    /// <summary>Null for a stock alert.</summary>
    public Guid? CustomerId { get; private set; }

    public NotificationChannel Channel { get; private set; }

    /// <summary>A phone number in E.164 for SMS, an email address for email.</summary>
    public string Recipient { get; private set; } = string.Empty;

    /// <summary>ISO 639-1, e.g. <c>bn</c>.</summary>
    public string Language { get; private set; } = string.Empty;

    /// <summary>Email only.</summary>
    public string? Subject { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public NotificationStatus Status { get; private set; }

    /// <summary>Tries started so far.</summary>
    public int Attempts { get; private set; }

    /// <summary>When the message may next be tried; null once it is sent or has failed.</summary>
    public DateTimeOffset? NextAttemptAt { get; private set; }

    /// <summary>Why the latest try failed; null when it has not failed.</summary>
    public string? LastError { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    /// <summary>Concurrency token (PostgreSQL xmin), so two senders cannot both take the same try.</summary>
    public uint RowVersion { get; private set; }

    /// <summary>A message to the shopper about their order, ready for its first try now.</summary>
    public static NotificationLog Create(
        Guid eventId,
        NotificationKind kind,
        Guid orderId,
        string orderNumber,
        Guid customerId,
        NotificationChannel channel,
        string recipient,
        string language,
        string? subject,
        string body,
        DateTimeOffset now)
    {
        var log = New(eventId, kind, channel, recipient, language, subject, body, now);
        log.OrderId = orderId;
        log.OrderNumber = orderNumber;
        log.CustomerId = customerId;
        return log;
    }

    /// <summary>A stock alert to the store's contact, ready for its first try now.</summary>
    /// <param name="eventId">See <see cref="EventId"/>.</param>
    public static NotificationLog CreateStaffAlert(
        Guid eventId,
        NotificationKind kind,
        NotificationChannel channel,
        string recipient,
        string language,
        string? subject,
        string body,
        DateTimeOffset now) => New(eventId, kind, channel, recipient, language, subject, body, now);

    /// <summary>
    /// Starts a try when one is due, and holds the message for <see cref="RetryPolicy.ClaimTimeout"/> so no other
    /// sender starts one meanwhile. False when it is sent, has failed, or is not due yet.
    /// </summary>
    public bool TryClaim(RetryPolicy policy, DateTimeOffset now)
    {
        if (Status != NotificationStatus.Pending || NextAttemptAt > now)
        {
            return false;
        }

        Attempts++;
        NextAttemptAt = now + policy.ClaimTimeout;
        return true;
    }

    public void MarkSent(DateTimeOffset now)
    {
        Status = NotificationStatus.Sent;
        SentAt = now;
        NextAttemptAt = null;
        LastError = null;
    }

    /// <summary>The try failed: the message waits for a retry, or fails for good after the last try.</summary>
    public void RecordFailure(string error, RetryPolicy policy, DateTimeOffset now)
    {
        LastError = Clip(error, MaxErrorLength);
        if (Attempts >= policy.MaxAttempts)
        {
            Status = NotificationStatus.Failed;
            NextAttemptAt = null;
            return;
        }

        NextAttemptAt = now + policy.DelayAfter(Attempts);
    }

    private static NotificationLog New(
        Guid eventId,
        NotificationKind kind,
        NotificationChannel channel,
        string recipient,
        string language,
        string? subject,
        string body,
        DateTimeOffset now) => new()
        {
            Id = Guid.CreateVersion7(),
            EventId = eventId,
            Kind = kind,
            Channel = channel,
            Recipient = recipient,
            Language = language,
            Subject = channel == NotificationChannel.Email ? Clip(subject, MaxSubjectLength) : null,
            Body = Clip(body, MaxBodyLength)!,
            Status = NotificationStatus.Pending,
            CreatedAt = now,
            NextAttemptAt = now,
        };

    private static string? Clip(string? value, int maxLength) =>
        value is { Length: > 0 } && value.Length > maxLength ? value[..maxLength] : value;
}
