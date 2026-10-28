using ClarioShop.Settings.Domain;
using ClarioShop.Settings.Persistence;
using ClarioShop.Settings.Services;

namespace ClarioShop.Settings.Features.DeliveryZones;

/// <summary>
/// Deletes a zone. Orders copy the zone's name and fee when placed, so their history is unaffected; to stop
/// offering a zone for a while, deactivate it instead.
/// </summary>
public static class DeleteDeliveryZone
{
    public sealed record Command(Guid Id);

    internal sealed class Handler(SettingsDbContext db, SettingsCache cache) : ICommandHandler<Command, Guid>
    {
        public async Task<Result<Guid>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var deleted = await db.DeliveryZones.Where(zone => zone.Id == command.Id).ExecuteDeleteAsync(cancellationToken);
            if (deleted == 0)
            {
                return StoreSettingsErrors.DeliveryZoneNotFound;
            }

            await cache.InvalidateAsync(cancellationToken);
            return command.Id;
        }
    }
}
