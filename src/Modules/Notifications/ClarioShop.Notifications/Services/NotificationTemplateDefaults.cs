using ClarioShop.Notifications.Contracts;
using ClarioShop.Notifications.Domain;

namespace ClarioShop.Notifications.Services;

internal sealed class NotificationTemplateDefaults : INotificationTemplateDefaults
{
    private static readonly IReadOnlyList<NotificationTemplateDefinition> Definitions = DefaultTemplates.All
        .OrderBy(entry => entry.Kind).ThenBy(entry => entry.Channel).ThenBy(entry => entry.Language, StringComparer.Ordinal)
        .Select(entry => new NotificationTemplateDefinition(
            entry.Kind.ToString(),
            entry.Channel.ToString(),
            entry.Language,
            entry.Template.Subject,
            entry.Template.Body,
            MessagePlaceholders.For(entry.Kind)))
        .ToList();

    public IReadOnlyList<NotificationTemplateDefinition> All => Definitions;
}
