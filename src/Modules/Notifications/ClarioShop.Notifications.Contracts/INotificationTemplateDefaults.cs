namespace ClarioShop.Notifications.Contracts;

/// <summary>
/// The messages Notifications can send and its built-in text for each, so Settings can list them, check a store's
/// own template before saving it and reset it to the built-in one.
/// </summary>
public interface INotificationTemplateDefaults
{
    /// <summary>One per event, channel and language.</summary>
    IReadOnlyList<NotificationTemplateDefinition> All { get; }
}
