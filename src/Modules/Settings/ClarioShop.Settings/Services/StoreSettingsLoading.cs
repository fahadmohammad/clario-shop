using ClarioShop.Infrastructure.Tenancy;
using ClarioShop.Settings.Domain;
using ClarioShop.Settings.Persistence;

namespace ClarioShop.Settings.Services;

internal static class StoreSettingsLoading
{
    /// <summary>
    /// The tenant's settings, or new defaults named after the tenant (added to the context, not yet saved)
    /// when it has never saved any.
    /// </summary>
    public static async Task<StoreSettings> LoadOrCreateAsync(this SettingsDbContext db, TenantContext tenant, TimeProvider clock, CancellationToken cancellationToken)
    {
        var settings = await db.StoreSettings.FirstOrDefaultAsync(s => s.Id == StoreSettings.SingletonId, cancellationToken);
        if (settings is null)
        {
            settings = StoreSettings.CreateDefault(tenant.Current.Name, clock.GetUtcNow());
            db.StoreSettings.Add(settings);
        }

        return settings;
    }
}
