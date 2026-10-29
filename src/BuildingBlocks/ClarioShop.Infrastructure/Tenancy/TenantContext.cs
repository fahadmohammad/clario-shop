using ClarioShop.SharedKernel.Tenancy;

namespace ClarioShop.Infrastructure.Tenancy;

/// <summary>Scoped holder of the tenant for the current request or background unit of work. Set once per scope.</summary>
public sealed class TenantContext : ITenantContext
{
    private TenantInfo? tenant;

    public bool IsResolved => tenant is not null;

    public Guid TenantId => Current.Id;

    public string Identifier => Current.Identifier;

    public string ConnectionString => Current.ConnectionString;

    public PlanFeatures Features => Current.Features;

    public TenantInfo Current => tenant ?? throw new InvalidOperationException("No tenant has been resolved for this scope.");

    public void Set(TenantInfo value)
    {
        if (tenant is not null && tenant.Id != value.Id)
        {
            throw new InvalidOperationException("The tenant of a scope cannot change once it is set.");
        }

        tenant = value;
    }
}
