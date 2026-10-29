using ClarioShop.Catalog.Domain;
using ClarioShop.Infrastructure.Auditing;
using ClarioShop.Infrastructure.Persistence;

namespace ClarioShop.Catalog.Persistence;

internal sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "catalog";

    public override string Schema => SchemaName;

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Brand> Brands => Set<Brand>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Variant> Variants => Set<Variant>();

    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.MapAuditLog(Schema);
    }
}
