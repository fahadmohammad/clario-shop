using ClarioShop.Platform.Domain;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;

namespace ClarioShop.Platform.Persistence;

/// <summary>
/// The shared platform database: tenant registry and the Data Protection key ring (later also plans and client
/// billing). Not tenant-scoped.
/// </summary>
internal sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public const string SchemaName = "platform";

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<TenantHost> TenantHosts => Set<TenantHost>();

    /// <summary>Keys every instance of the API shares, so data protected by one (e.g. merchant passwords) the others can read.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlatformDbContext).Assembly);
    }
}
