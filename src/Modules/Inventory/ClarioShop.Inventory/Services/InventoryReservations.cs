using ClarioShop.Inventory.Contracts;
using ClarioShop.Inventory.Domain;
using ClarioShop.Inventory.Persistence;

namespace ClarioShop.Inventory.Services;

/// <summary>Checkout's synchronous entry point into Inventory (see <see cref="IInventoryReservations"/>).</summary>
internal sealed class InventoryReservations(
    InventoryDbContext db,
    LocationResolver locations,
    ItemRegistry items,
    ReservationExpiry expiry,
    TimeProvider clock) : IInventoryReservations
{
    public async Task<Result<StockReservationInfo>> ReserveAsync(ReserveStockRequest request, CancellationToken cancellationToken)
    {
        var variantIds = request.Lines.Select(line => line.VariantId).Distinct().ToList();

        return await ConcurrencyRetry.ExecuteAsync<StockReservationInfo>(db, async ct =>
        {
            var reference = StockReference.Order(request.OrderId);
            var existing = await db.Reservations.AsNoTracking().SingleOrDefaultAsync(
                r => r.ReferenceType == reference.Type && r.ReferenceId == reference.Id && r.Status == ReservationStatus.Active,
                ct);
            if (existing is not null)
            {
                return ToInfo(existing);
            }

            var location = await locations.ResolveAsync(request.LocationId, ct);
            if (location.IsFailure)
            {
                return location.Error;
            }

            var known = await items.EnsureKnownAsync(variantIds, ct);
            if (known.IsFailure)
            {
                return known.Error;
            }

            var now = clock.GetUtcNow();

            // Free stock held by reservations that ran out but were not swept yet, so it can be sold now.
            await expiry.ExpireDueAsync(now, variantIds, ct);

            var created = StockReservation.Create(
                reference,
                location.Value,
                request.Lines.Select(line => (line.VariantId, line.Quantity)).ToList(),
                IInventoryReservations.ReservationLifetime,
                now);
            if (created.IsFailure)
            {
                return created.Error;
            }

            var reservation = created.Value;
            var levels = await db.LoadOrCreateAsync(location.Value, variantIds, now, ct);
            foreach (var line in reservation.Lines)
            {
                var reserved = levels[line.VariantId].Reserve(line.Quantity, now);
                if (reserved.IsFailure)
                {
                    return reserved.Error;
                }
            }

            db.Reservations.Add(reservation);
            await db.SaveChangesAsync(ct);
            return ToInfo(reservation);
        }, cancellationToken);
    }

    public async Task<Result> CommitAsync(Guid reservationId, CancellationToken cancellationToken) =>
        await ConcurrencyRetry.ExecuteAsync<bool>(db, async ct =>
        {
            var reservation = await db.Reservations.SingleOrDefaultAsync(r => r.Id == reservationId, ct);
            if (reservation is null)
            {
                return InventoryErrors.ReservationNotFound;
            }

            if (reservation.Status == ReservationStatus.Committed)
            {
                return true;
            }

            var now = clock.GetUtcNow();
            if (reservation.IsDue(now))
            {
                await expiry.ExpireAsync(reservation, now, ct);
                await db.SaveChangesAsync(ct);
                return InventoryErrors.ReservationExpired;
            }

            var committed = reservation.Commit(now);
            if (committed.IsFailure)
            {
                return committed.Error;
            }

            var levels = await db.LoadOrCreateAsync(reservation.LocationId, reservation.Lines.Select(l => l.VariantId).ToList(), now, ct);
            foreach (var line in reservation.Lines)
            {
                var sale = StockMovement.Record(
                    line.VariantId,
                    reservation.LocationId,
                    MovementType.OnlineSale,
                    -line.Quantity,
                    StockReference.Order(reservation.ReferenceId),
                    now,
                    now,
                    userId: null).Value;
                db.Movements.Add(sale);
                levels[line.VariantId].CommitReservation(sale);
            }

            await db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

    public async Task<Result> ReleaseAsync(Guid reservationId, CancellationToken cancellationToken) =>
        await ConcurrencyRetry.ExecuteAsync<bool>(db, async ct =>
        {
            var reservation = await db.Reservations.SingleOrDefaultAsync(r => r.Id == reservationId, ct);
            if (reservation is null)
            {
                return InventoryErrors.ReservationNotFound;
            }

            var now = clock.GetUtcNow();
            var released = reservation.Release(now);
            if (released.IsFailure || !released.Value)
            {
                return released;
            }

            await ReturnToStockAsync(db, reservation, now, ct);
            await db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

    /// <summary>Drops a closed reservation's hold from the stock levels.</summary>
    internal static async Task ReturnToStockAsync(InventoryDbContext db, StockReservation reservation, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var levels = await db.LoadOrCreateAsync(reservation.LocationId, reservation.Lines.Select(l => l.VariantId).ToList(), now, cancellationToken);
        foreach (var line in reservation.Lines)
        {
            levels[line.VariantId].Release(line.Quantity, now);
        }
    }

    private static StockReservationInfo ToInfo(StockReservation reservation) => new(
        reservation.Id,
        reservation.ReferenceId,
        reservation.LocationId,
        reservation.ExpiresAt,
        reservation.Lines.Select(line => new ReservationLine(line.VariantId, line.Quantity)).ToList());
}
