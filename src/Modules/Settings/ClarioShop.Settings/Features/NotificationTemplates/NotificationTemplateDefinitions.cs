using ClarioShop.Notifications.Contracts;

namespace ClarioShop.Settings.Features.NotificationTemplates;

internal static class NotificationTemplateDefinitions
{
    /// <summary>The message named in a URL, ignoring case; null when Notifications has no such message.</summary>
    public static NotificationTemplateDefinition? Find(this INotificationTemplateDefaults defaults, string eventName, string channel, string language) =>
        defaults.All.FirstOrDefault(definition =>
            string.Equals(definition.Event, eventName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(definition.Channel, channel, StringComparison.OrdinalIgnoreCase)
            && string.Equals(definition.Language, language, StringComparison.OrdinalIgnoreCase));

    public static bool IsEmail(this NotificationTemplateDefinition definition) => definition.Subject is not null;
}
