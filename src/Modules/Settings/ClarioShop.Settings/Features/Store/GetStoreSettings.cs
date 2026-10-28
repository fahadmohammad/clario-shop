using ClarioShop.Infrastructure.Tenancy;
using ClarioShop.Settings.Contracts;
using ClarioShop.Settings.Domain;
using ClarioShop.Settings.Persistence;
using ClarioShop.Settings.Services;

namespace ClarioShop.Settings.Features.Store;

/// <summary>All store settings for the back office, read from the database rather than the cache.</summary>
public static class GetStoreSettings
{
    public sealed record Query;

    internal sealed class Handler(SettingsDbContext db, TenantContext tenant, TimeProvider clock) : ICommandHandler<Query, StoreSettingsInfo>
    {
        public async Task<Result<StoreSettingsInfo>> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            var settings = await db.StoreSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == StoreSettings.SingletonId, cancellationToken)
                ?? StoreSettings.CreateDefault(tenant.Current.Name, clock.GetUtcNow());
            return settings.ToInfo();
        }
    }
}
