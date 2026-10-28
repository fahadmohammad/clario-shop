using ClarioShop.Infrastructure.Persistence;
using ClarioShop.Reporting.Domain;

namespace ClarioShop.Reporting.Persistence;

internal sealed class ReportingDbContext(DbContextOptions<ReportingDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "reporting";

    public override string Schema => SchemaName;

    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
}
