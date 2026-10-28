using ClarioShop.Notifications.Contracts;
using ClarioShop.Settings.Persistence;

namespace ClarioShop.Settings.Features.NotificationTemplates;

/// <summary>Every message the shopper can get, in every language, with the store's own text where it has one.</summary>
public static class ListNotificationTemplates
{
    public sealed record Query;

    internal sealed class Handler(SettingsDbContext db, INotificationTemplateDefaults defaults) : ICommandHandler<Query, IReadOnlyList<NotificationTemplateResponse>>
    {
        public async Task<Result<IReadOnlyList<NotificationTemplateResponse>>> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            var custom = await db.NotificationTemplates.AsNoTracking().ToListAsync(cancellationToken);
            var byKey = custom.ToDictionary(template => (template.Event, template.Channel, template.Language));
            return defaults.All
                .Select(definition => NotificationTemplateResponse.From(definition, byKey.GetValueOrDefault((definition.Event, definition.Channel, definition.Language))))
                .ToList();
        }
    }
}
