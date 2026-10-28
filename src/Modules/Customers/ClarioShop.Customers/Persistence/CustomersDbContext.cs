using ClarioShop.Customers.Domain;
using ClarioShop.Infrastructure.Persistence;

namespace ClarioShop.Customers.Persistence;

internal sealed class CustomersDbContext(DbContextOptions<CustomersDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "customers";

    public override string Schema => SchemaName;

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Address> Addresses => Set<Address>();
}
