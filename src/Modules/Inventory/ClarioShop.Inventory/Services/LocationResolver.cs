using ClarioShop.Inventory.Domain;
using ClarioShop.Inventory.Persistence;

namespace ClarioShop.Inventory.Services;

internal sealed class LocationResolver(InventoryDbContext db)
{
    /// <summary>The requested active location, or the tenant's default location when none is given.</summary>
    public async Task<Result<Guid>> ResolveAsync(Guid? locationId, CancellationToken cancellationToken)
    {
        var found = locationId is { } id
            ? await db.Locations.Where(l => l.Id == id && l.IsActive).Select(l => (Guid?)l.Id).SingleOrDefaultAsync(cancellationToken)
            : await db.Locations.Where(l => l.IsDefault).Select(l => (Guid?)l.Id).SingleOrDefaultAsync(cancellationToken);

        return found is { } resolved ? resolved : InventoryErrors.LocationNotFound;
    }
}
