using ClarioShop.Platform.Persistence;

namespace ClarioShop.Platform.Features.Tenants;

public static class ListTenants
{
    public sealed record Query;

    public sealed record TenantItem(Guid Id, string Identifier, string Name, bool IsActive, DateTimeOffset CreatedAt, IReadOnlyList<string> Hosts);

    internal sealed class Handler(PlatformDbContext db) : ICommandHandler<Query, IReadOnlyList<TenantItem>>
    {
        public async Task<Result<IReadOnlyList<TenantItem>>> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            var tenants = await db.Tenants.AsNoTracking()
                .OrderBy(tenant => tenant.Identifier)
                .Select(tenant => new TenantItem(
                    tenant.Id,
                    tenant.Identifier,
                    tenant.Name,
                    tenant.IsActive,
                    tenant.CreatedAt,
                    tenant.Hosts.OrderBy(host => host.Host).Select(host => host.Host).ToList()))
                .ToListAsync(cancellationToken);

            return tenants;
        }
    }
}
