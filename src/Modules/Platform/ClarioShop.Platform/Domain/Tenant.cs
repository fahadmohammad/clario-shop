using ClarioShop.SharedKernel.Domain;

namespace ClarioShop.Platform.Domain;

/// <summary>A client business using Clario Shop. Each tenant has its own database.</summary>
internal sealed class Tenant : Entity
{
    private readonly List<TenantHost> hosts = [];

    private Tenant()
    {
    }

    /// <summary>Short, URL-safe id, e.g. "rahman-store". Also used in the tenant's database name.</summary>
    public string Identifier { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string ConnectionString { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public TenantPlan Plan { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<TenantHost> Hosts => hosts;

    public static Tenant Create(string identifier, string name, string connectionString, IEnumerable<string> hostNames, TenantPlan plan, DateTimeOffset now)
    {
        var tenant = new Tenant
        {
            Id = Guid.CreateVersion7(),
            Identifier = identifier,
            Name = name.Trim(),
            ConnectionString = connectionString,
            IsActive = false,
            Plan = plan,
            CreatedAt = now,
        };

        tenant.hosts.AddRange(hostNames.Select(TenantHost.Normalize).Distinct().Select(host => new TenantHost { Host = host, TenantId = tenant.Id }));
        return tenant;
    }

    /// <summary>Called once the tenant database is migrated and the owner account exists.</summary>
    public void Activate() => IsActive = true;

    public void ChangePlan(TenantPlan plan) => Plan = plan;
}
