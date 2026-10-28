using ClarioShop.Infrastructure.Tenancy;
using ClarioShop.Settings.Contracts;
using ClarioShop.Settings.Domain;
using ClarioShop.Settings.Persistence;

namespace ClarioShop.Settings.Services;

internal sealed class SettingsQueries(SettingsDbContext db, SettingsCache cache, TenantContext tenant, TimeProvider clock) : ISettingsQueries
{
    public async Task<StoreSettingsInfo> GetStoreSettingsAsync(CancellationToken cancellationToken) =>
        await cache.Cache.GetOrCreateAsync(
            cache.StoreKey,
            async token =>
            {
                var settings = await db.StoreSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == StoreSettings.SingletonId, token)
                    ?? StoreSettings.CreateDefault(tenant.Current.Name, clock.GetUtcNow());
                return settings.ToInfo();
            },
            SettingsCache.Options,
            [cache.Tag],
            cancellationToken);

    public async Task<IReadOnlyList<DeliveryZoneInfo>> GetDeliveryZonesAsync(CancellationToken cancellationToken) =>
        await cache.Cache.GetOrCreateAsync(
            cache.DeliveryZonesKey,
            async token =>
            {
                var zones = await db.DeliveryZones.AsNoTracking()
                    .Where(zone => zone.IsActive)
                    .OrderBy(zone => zone.SortOrder).ThenBy(zone => zone.Name)
                    .ToListAsync(token);
                return zones.Select(zone => zone.ToInfo()).ToList();
            },
            SettingsCache.Options,
            [cache.Tag],
            cancellationToken);

    public async Task<IReadOnlyList<NotificationTemplateInfo>> GetNotificationTemplatesAsync(CancellationToken cancellationToken) =>
        await cache.Cache.GetOrCreateAsync(
            cache.NotificationTemplatesKey,
            async token =>
            {
                var templates = await db.NotificationTemplates.AsNoTracking().ToListAsync(token);
                return templates.Select(template => template.ToInfo()).ToList();
            },
            SettingsCache.Options,
            [cache.Tag],
            cancellationToken);

    public async Task<DeliveryZoneInfo?> GetDeliveryZoneAsync(Guid deliveryZoneId, CancellationToken cancellationToken) =>
        (await GetDeliveryZonesAsync(cancellationToken)).FirstOrDefault(zone => zone.Id == deliveryZoneId);
}
