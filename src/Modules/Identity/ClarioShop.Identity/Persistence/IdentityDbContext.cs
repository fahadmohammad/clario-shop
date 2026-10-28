using ClarioShop.Identity.Domain;
using ClarioShop.Infrastructure.Persistence;

namespace ClarioShop.Identity.Persistence;

internal sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : ModuleDbContext(options)
{
    public const string SchemaName = "identity";

    public override string Schema => SchemaName;

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // OpenIddict applications, authorizations, scopes and tokens, in this schema.
        modelBuilder.UseOpenIddict<Guid>();
    }
}
