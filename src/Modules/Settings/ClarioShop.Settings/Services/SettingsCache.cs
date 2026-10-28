using ClarioShop.SharedKernel.Tenancy;
using Microsoft.Extensions.Caching.Hybrid;

namespace ClarioShop.Settings.Services;

/// <summary>Cache keys for the current tenant's settings, all under one tag so any change drops them together.</summary>
internal sealed class SettingsCache(HybridCache cache, ITenantContext tenant)
{
    public static readonly HybridCacheEntryOptions Options = new()
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(1),
    };

    public HybridCache Cache => cache;

    public string Tag => $"settings:{tenant.TenantId:N}";

    public string StoreKey => $"{Tag}:store";

    public string DeliveryZonesKey => $"{Tag}:delivery-zones";

    /// <summary>Call after a change is saved, so the next read sees it.</summary>
    public ValueTask InvalidateAsync(CancellationToken cancellationToken) => cache.RemoveByTagAsync(Tag, cancellationToken);
}
