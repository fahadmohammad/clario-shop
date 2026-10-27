using ClarioShop.Inventory.Persistence;

namespace ClarioShop.Inventory.Features.Locations;

public static class ListLocations
{
    public sealed record Query;

    public sealed record LocationItem(Guid Id, string Name, string Code, string? Address, bool IsDefault, bool IsActive);

    internal sealed class Handler(InventoryDbContext db) : ICommandHandler<Query, IReadOnlyList<LocationItem>>
    {
        public async Task<Result<IReadOnlyList<LocationItem>>> HandleAsync(Query query, CancellationToken cancellationToken) =>
            Result.Success<IReadOnlyList<LocationItem>>(await db.Locations.AsNoTracking()
                .OrderByDescending(l => l.IsDefault)
                .ThenBy(l => l.Name)
                .Select(l => new LocationItem(l.Id, l.Name, l.Code, l.Address, l.IsDefault, l.IsActive))
                .ToListAsync(cancellationToken));
    }
}
