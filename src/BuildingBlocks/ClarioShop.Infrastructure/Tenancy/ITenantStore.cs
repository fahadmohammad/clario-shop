namespace ClarioShop.Infrastructure.Tenancy;

/// <summary>Looks up tenants in the platform registry. Implemented by the Platform module.</summary>
public interface ITenantStore
{
    Task<TenantInfo?> FindByHostAsync(string host, CancellationToken cancellationToken);

    Task<TenantInfo?> FindByIdentifierAsync(string identifier, CancellationToken cancellationToken);

    Task<TenantInfo?> FindByIdAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TenantInfo>> GetActiveTenantsAsync(CancellationToken cancellationToken);
}
