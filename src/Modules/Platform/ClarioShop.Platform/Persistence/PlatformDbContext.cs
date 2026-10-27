using ClarioShop.Platform.Domain;

namespace ClarioShop.Platform.Persistence;

/// <summary>The shared platform database: tenant registry (later also plans and client billing). Not tenant-scoped.</summary>
internal sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options)
{
    public const string SchemaName = "platform";

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<TenantHost> TenantHosts => Set<TenantHost>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlatformDbContext).Assembly);
    }
}
