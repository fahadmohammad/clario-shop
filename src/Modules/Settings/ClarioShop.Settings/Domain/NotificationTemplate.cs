using ClarioShop.SharedKernel.Domain;

namespace ClarioShop.Settings.Domain;

/// <summary>
/// The store's own text for one message the shopper gets (event, channel and language), in place of the built-in
/// one in Notifications. One per message; deleting it brings the built-in text back.
/// </summary>
internal sealed class NotificationTemplate : Entity
{
    public const int MaxSubjectLength = 200;

    /// <summary>Seven Unicode SMS parts; Bangla needs Unicode.</summary>
    public const int MaxSmsBodyLength = 470;

    public const int MaxEmailBodyLength = 5000;

    private NotificationTemplate()
    {
    }

    /// <summary>E.g. <c>OrderPlaced</c>.</summary>
    public string Event { get; private set; } = string.Empty;

    /// <summary><c>Sms</c> or <c>Email</c>.</summary>
    public string Channel { get; private set; } = string.Empty;

    /// <summary>E.g. <c>bn</c> or <c>en</c>.</summary>
    public string Language { get; private set; } = string.Empty;

    /// <summary>Email only; null for SMS.</summary>
    public string? Subject { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Concurrency token (PostgreSQL xmin).</summary>
    public uint RowVersion { get; private set; }

    public static NotificationTemplate Create(string eventName, string channel, string language, string? subject, string body, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        Event = eventName,
        Channel = channel,
        Language = language,
        Subject = subject,
        Body = body,
        CreatedAt = now,
        UpdatedAt = now,
    };

    public void Update(string? subject, string body, DateTimeOffset now)
    {
        Subject = subject;
        Body = body;
        UpdatedAt = now;
    }
}
