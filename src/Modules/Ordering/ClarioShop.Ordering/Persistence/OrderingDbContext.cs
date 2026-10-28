using ClarioShop.Infrastructure.Persistence;
using ClarioShop.Ordering.Domain;

namespace ClarioShop.Ordering.Persistence;

internal sealed class OrderingDbContext(DbContextOptions<OrderingDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "ordering";

    /// <summary>The tenant's order number sequence (one database per tenant, so numbers are per tenant).</summary>
    public const string OrderNumberSequence = "OrderNumbers";

    public override string Schema => SchemaName;

    public DbSet<Cart> Carts => Set<Cart>();

    public DbSet<Order> Orders => Set<Order>();

    /// <summary>Takes the next order number. Sequences are not transactional: a checkout that fails afterwards leaves a gap.</summary>
    public async Task<long> NextOrderNumberAsync(CancellationToken cancellationToken) =>
        await Database.SqlQueryRaw<long>($"SELECT nextval('{SchemaName}.\"{OrderNumberSequence}\"') AS \"Value\"")
            .SingleAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasSequence<long>(OrderNumberSequence).StartsAt(1);
    }
}
