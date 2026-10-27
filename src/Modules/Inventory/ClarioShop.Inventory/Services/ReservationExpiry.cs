using ClarioShop.Inventory.Domain;
using ClarioShop.Inventory.Persistence;

namespace ClarioShop.Inventory.Services;

/// <summary>Expires reservations whose 15 minutes are up and gives their stock back.</summary>
internal sealed class ReservationExpiry(InventoryDbContext db)
{
    private const int BatchSize = 100;

    /// <summary>
    /// Expires due reservations (optionally only those holding one of <paramref name="variantIds"/>) and saves.
    /// Returns how many were expired.
    /// </summary>
    public async Task<int> ExpireDueAsync(DateTimeOffset now, IReadOnlyCollection<Guid>? variantIds, CancellationToken cancellationToken)
    {
        var due = db.Reservations.Where(r => r.Status == ReservationStatus.Active && r.ExpiresAt <= now);
        if (variantIds is not null)
        {
            due = due.Where(r => r.Lines.Any(line => variantIds.Contains(line.VariantId)));
        }

        var reservations = await due.OrderBy(r => r.ExpiresAt).Take(BatchSize).ToListAsync(cancellationToken);
        foreach (var reservation in reservations)
        {
            await ExpireAsync(reservation, now, cancellationToken);
        }

        if (reservations.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return reservations.Count;
    }

    /// <summary>Expires one tracked reservation and releases its stock, without saving.</summary>
    public async Task ExpireAsync(StockReservation reservation, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (reservation.Expire(now))
        {
            await InventoryReservations.ReturnToStockAsync(db, reservation, now, cancellationToken);
        }
    }
}
