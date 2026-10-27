using ClarioShop.Infrastructure.Tenancy;
using ClarioShop.Inventory.Domain;
using ClarioShop.Inventory.Persistence;

namespace ClarioShop.Inventory.Services;

/// <summary>Gives every tenant its default location. Idempotent: runs after each migration.</summary>
internal sealed class DefaultLocationInitializer(InventoryDbContext db, TimeProvider clock) : ITenantInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (await db.Locations.AnyAsync(location => location.IsDefault, cancellationToken))
        {
            return;
        }

        db.Locations.Add(Location.CreateDefault(clock.GetUtcNow()));
        await db.SaveChangesAsync(cancellationToken);
    }
}
