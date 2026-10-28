using ClarioShop.Settings.Domain;
using ClarioShop.Settings.Persistence;
using ClarioShop.Settings.Services;

namespace ClarioShop.Settings.Features.DeliveryZones;

/// <summary>Replaces a zone. Orders already placed keep the fee they were charged.</summary>
public static class UpdateDeliveryZone
{
    public sealed record Command(Guid Id, DeliveryZoneInput Zone);

    internal sealed class Handler(SettingsDbContext db, SettingsCache cache, TimeProvider clock) : ICommandHandler<Command, DeliveryZoneResponse>
    {
        public async Task<Result<DeliveryZoneResponse>> HandleAsync(Command command, CancellationToken cancellationToken)
        {
            var zone = await db.DeliveryZones.FirstOrDefaultAsync(z => z.Id == command.Id, cancellationToken);
            if (zone is null)
            {
                return StoreSettingsErrors.DeliveryZoneNotFound;
            }

            if (await db.NameTakenAsync(command.Zone.Name, command.Id, cancellationToken))
            {
                return StoreSettingsErrors.DeliveryZoneNameTaken(command.Zone.Name.Trim());
            }

            zone.Apply(command.Zone.ToSpec(), clock.GetUtcNow());
            return await db.SaveZoneAsync(zone, cache, cancellationToken);
        }
    }
}
