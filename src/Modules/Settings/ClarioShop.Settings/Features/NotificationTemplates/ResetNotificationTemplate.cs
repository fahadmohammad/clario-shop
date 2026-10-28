using ClarioShop.Notifications.Contracts;
using ClarioShop.Settings.Domain;
using ClarioShop.Settings.Persistence;
using ClarioShop.Settings.Services;

namespace ClarioShop.Settings.Features.NotificationTemplates;

/// <summary>Deletes the store's own text for one message, so it sends the built-in text again. Resetting twice is harmless.</summary>
public static class ResetNotificationTemplate
{
    public sealed record Command(string Event, string Channel, string Language);

    internal sealed class Handler(SettingsDbContext db, INotificationTemplateDefaults defaults, SettingsCache cache)
        : ICommandHandler<Command, NotificationTemplateResponse>
    {
        public async Task<Result<NotificationTemplateResponse>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            if (defaults.Find(command.Event, command.Channel, command.Language) is not { } definition)
            {
                return StoreSettingsErrors.NotificationTemplateNotFound;
            }

            await db.NotificationTemplates
                .Where(t => t.Event == definition.Event && t.Channel == definition.Channel && t.Language == definition.Language)
                .ExecuteDeleteAsync(cancellationToken);
            await cache.InvalidateAsync(cancellationToken);
            return NotificationTemplateResponse.From(definition, null);
        }
    }
}
