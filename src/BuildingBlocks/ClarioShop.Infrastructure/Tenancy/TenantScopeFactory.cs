namespace ClarioShop.Infrastructure.Tenancy;

/// <summary>Creates a DI scope bound to one tenant, for background work (migrations, outbox, consumers).</summary>
public sealed class TenantScopeFactory(IServiceScopeFactory scopeFactory)
{
    public AsyncServiceScope CreateScope(TenantInfo tenant)
    {
        var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);
        return scope;
    }
}
