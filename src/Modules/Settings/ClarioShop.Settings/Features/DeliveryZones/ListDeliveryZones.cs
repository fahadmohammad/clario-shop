using ClarioShop.Settings.Persistence;

namespace ClarioShop.Settings.Features.DeliveryZones;

/// <summary>Every delivery zone, active or not, in display order.</summary>
public static class ListDeliveryZones
{
    public sealed record Query;

    internal sealed class Handler(SettingsDbContext db) : ICommandHandler<Query, IReadOnlyList<DeliveryZoneResponse>>
    {
        public async Task<Result<IReadOnlyList<DeliveryZoneResponse>>> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            var zones = await db.DeliveryZones.AsNoTracking()
                .OrderBy(zone => zone.SortOrder).ThenBy(zone => zone.Name)
                .ToListAsync(cancellationToken);
            return zones.Select(DeliveryZoneResponse.From).ToList();
        }
    }
}
