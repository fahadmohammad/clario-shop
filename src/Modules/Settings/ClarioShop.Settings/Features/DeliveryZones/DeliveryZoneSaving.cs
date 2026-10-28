using ClarioShop.Settings.Domain;
using ClarioShop.Settings.Persistence;
using ClarioShop.Settings.Services;
using Npgsql;

namespace ClarioShop.Settings.Features.DeliveryZones;

internal static class DeliveryZoneSaving
{
    public static Task<bool> NameTakenAsync(this SettingsDbContext db, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var normalized = DeliveryZone.Normalize(name);
        return db.DeliveryZones.AnyAsync(zone => zone.NormalizedName == normalized && zone.Id != exceptId, cancellationToken);
    }

    /// <summary>
    /// Saves zone changes and drops the cached settings. A name taken concurrently, or a zone changed concurrently,
    /// is a conflict.
    /// </summary>
    public static async Task<Result<DeliveryZoneResponse>> SaveZoneAsync(
        this SettingsDbContext db, DeliveryZone zone, SettingsCache cache, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return StoreSettingsErrors.DeliveryZoneNameTaken(zone.Name);
        }
        catch (DbUpdateConcurrencyException)
        {
            return StoreSettingsErrors.Concurrency;
        }

        await cache.InvalidateAsync(cancellationToken);
        return DeliveryZoneResponse.From(zone);
    }
}
