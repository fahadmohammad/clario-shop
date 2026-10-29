using ClarioShop.Infrastructure.Tenancy;
using ClarioShop.Platform.Domain;
using ClarioShop.Platform.Persistence;
using Microsoft.Extensions.Caching.Hybrid;

namespace ClarioShop.Platform.Services;

internal sealed class TenantStore(PlatformDbContext db, HybridCache cache) : ITenantStore
{
    public const string CacheTag = "tenants";

    /// <summary>Bumped whenever <see cref="TenantInfo"/> changes shape, so entries cached by an older build are never read.</summary>
    private const string KeyPrefix = "tenant:v2";

    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(1),
        LocalCacheExpiration = TimeSpan.FromSeconds(30),
    };

    public async Task<TenantInfo?> FindByHostAsync(string host, CancellationToken cancellationToken)
    {
        var normalized = TenantHost.Normalize(host);
        return await cache.GetOrCreateAsync(
            $"{KeyPrefix}:host:{normalized}",
            async token => await Project(db.Tenants.Where(t => t.Hosts.Any(h => h.Host == normalized))).SingleOrDefaultAsync(token),
            CacheOptions,
            [CacheTag],
            cancellationToken);
    }

    public async Task<TenantInfo?> FindByIdentifierAsync(string identifier, CancellationToken cancellationToken)
    {
        var normalized = identifier.Trim().ToLowerInvariant();
        return await cache.GetOrCreateAsync(
            $"{KeyPrefix}:identifier:{normalized}",
            async token => await Project(db.Tenants.Where(t => t.Identifier == normalized)).SingleOrDefaultAsync(token),
            CacheOptions,
            [CacheTag],
            cancellationToken);
    }

    public async Task<TenantInfo?> FindByIdAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync(
            $"{KeyPrefix}:id:{tenantId:N}",
            async token => await Project(db.Tenants.Where(t => t.Id == tenantId)).SingleOrDefaultAsync(token),
            CacheOptions,
            [CacheTag],
            cancellationToken);

    public async Task<IReadOnlyList<TenantInfo>> GetActiveTenantsAsync(CancellationToken cancellationToken) =>
        await Project(db.Tenants.Where(t => t.IsActive).OrderBy(t => t.Identifier)).ToListAsync(cancellationToken);

    private static IQueryable<TenantInfo> Project(IQueryable<Tenant> tenants) =>
        tenants.AsNoTracking().Select(t => ToInfo(t.Id, t.Identifier, t.Name, t.ConnectionString, t.IsActive, t.Plan));

    public static TenantInfo ToInfo(Guid id, string identifier, string name, string connectionString, bool isActive, TenantPlan plan) =>
        new(id, identifier, name, connectionString, isActive, plan.ToString(), TenantPlans.FeaturesOf(plan));
}
