namespace ClarioShop.Notifications.Domain;

internal enum NotificationStatus
{
    /// <summary>Not sent yet: waiting for its first try, or for a retry after a failed one.</summary>
    Pending,

    /// <summary>Handed to the SMS gateway or email provider.</summary>
    Sent,

    /// <summary>Every try failed; it is not tried again.</summary>
    Failed,
}
