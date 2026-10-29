namespace ClarioShop.SharedKernel.Tenancy;

/// <summary>The tenant (client business) the current request or message belongs to.</summary>
public interface ITenantContext
{
    bool IsResolved { get; }

    Guid TenantId { get; }

    string Identifier { get; }

    string ConnectionString { get; }

    /// <summary>What the tenant's plan includes.</summary>
    PlanFeatures Features { get; }
}
