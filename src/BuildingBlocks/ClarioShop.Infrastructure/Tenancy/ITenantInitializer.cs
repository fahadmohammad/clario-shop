namespace ClarioShop.Infrastructure.Tenancy;

/// <summary>Runs inside a tenant's scope after its migrations (e.g. syncing system roles). Must be idempotent.</summary>
public interface ITenantInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken);
}
