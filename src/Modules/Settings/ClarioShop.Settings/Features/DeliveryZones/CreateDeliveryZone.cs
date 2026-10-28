using ClarioShop.Settings.Domain;
using ClarioShop.Settings.Persistence;
using ClarioShop.Settings.Services;

namespace ClarioShop.Settings.Features.DeliveryZones;

public static class CreateDeliveryZone
{
    internal sealed class Handler(SettingsDbContext db, SettingsCache cache, TimeProvider clock) : ICommandHandler<DeliveryZoneInput, DeliveryZoneResponse>
    {
        public async Task<Result<DeliveryZoneResponse>> HandleAsync(DeliveryZoneInput input, CancellationToken cancellationToken)
        {
            if (await db.NameTakenAsync(input.Name, exceptId: null, cancellationToken))
            {
                return StoreSettingsErrors.DeliveryZoneNameTaken(input.Name.Trim());
            }

            var zone = DeliveryZone.Create(input.ToSpec(), clock.GetUtcNow());
            db.DeliveryZones.Add(zone);
            return await db.SaveZoneAsync(zone, cache, cancellationToken);
        }
    }
}
