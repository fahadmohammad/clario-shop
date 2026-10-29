using System.Data;
using ClarioShop.Inventory.Contracts.Events;
using ClarioShop.Inventory.Domain;
using ClarioShop.Inventory.Persistence;

namespace ClarioShop.Inventory.Services;

/// <summary>
/// Recomputes every stock level of the tenant from the ledger (on hand) and the active reservations (reserved). A
/// level that disagrees is recorded as a <see cref="StockDrift"/>, set back to the computed figures and announced
/// with <see cref="StockDriftDetected"/>, all in one transaction.
/// </summary>
internal sealed class StockDriftCheck(InventoryDbContext db, TimeProvider clock)
{
    internal sealed record Outcome(Guid CheckId, int CheckedLevels, IReadOnlyList<StockDrift> Drifts);

    private readonly record struct Key(Guid VariantId, Guid LocationId);

    public async Task<Result<Outcome>> RunAsync(CancellationToken cancellationToken) =>
        await ConcurrencyRetry.ExecuteAsync<Outcome>(db, async ct =>
        {
            // One snapshot for the levels, the ledger and the reservations. Every movement or hold also updates its
            // level, so correcting a level that a sale changed meanwhile fails on the level's RowVersion (or the
            // snapshot) and the check runs again rather than "correcting" the sale away.
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
            var levels = await db.StockLevels.ToDictionaryAsync(level => new Key(level.VariantId, level.LocationId), ct);
            var ledger = await db.Movements
                .GroupBy(movement => new { movement.VariantId, movement.LocationId })
                .Select(group => new { group.Key.VariantId, group.Key.LocationId, OnHand = group.Sum(movement => movement.Quantity) })
                .ToDictionaryAsync(row => new Key(row.VariantId, row.LocationId), row => row.OnHand, ct);
            var held = await db.Reservations
                .Where(reservation => reservation.Status == ReservationStatus.Active)
                .SelectMany(reservation => reservation.Lines, (reservation, line) => new { line.VariantId, reservation.LocationId, line.Quantity })
                .GroupBy(row => new { row.VariantId, row.LocationId })
                .Select(group => new { group.Key.VariantId, group.Key.LocationId, Reserved = group.Sum(row => row.Quantity) })
                .ToDictionaryAsync(row => new Key(row.VariantId, row.LocationId), row => row.Reserved, ct);

            var now = clock.GetUtcNow();
            var checkId = Guid.CreateVersion7();
            var drifts = new List<StockDrift>();
            foreach (var key in levels.Keys.Union(ledger.Keys).Union(held.Keys))
            {
                if (!levels.TryGetValue(key, out var level))
                {
                    // Movements or holds without a level row: the level was lost, so it counts as zero.
                    level = StockLevel.Create(key.VariantId, key.LocationId, now);
                    db.StockLevels.Add(level);
                }

                var ledgerOnHand = ledger.GetValueOrDefault(key);
                var heldReserved = held.GetValueOrDefault(key);
                if (!level.DriftsFrom(ledgerOnHand, heldReserved))
                {
                    continue;
                }

                var drift = StockDrift.Record(checkId, level, ledgerOnHand, heldReserved, now);
                level.Correct(ledgerOnHand, heldReserved, now);
                db.Drifts.Add(drift);
                drifts.Add(drift);
            }

            if (drifts.Count > 0)
            {
                await PublishAsync(drifts, now, ct);
                await db.SaveChangesAsync(ct);
            }

            await transaction.CommitAsync(ct);
            return new Outcome(checkId, levels.Count, drifts);
        }, cancellationToken);

    private async Task PublishAsync(List<StockDrift> drifts, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var variantIds = drifts.Select(drift => drift.VariantId).Distinct().ToList();
        var skus = await db.Items.AsNoTracking()
            .Where(item => variantIds.Contains(item.VariantId))
            .ToDictionaryAsync(item => item.VariantId, item => item.Sku, cancellationToken);
        var codes = await db.Locations.AsNoTracking().ToDictionaryAsync(location => location.Id, location => location.Code, cancellationToken);
        foreach (var drift in drifts)
        {
            db.Publish(new StockDriftDetected(
                Guid.CreateVersion7(),
                now,
                drift.CheckId,
                drift.VariantId,
                skus.GetValueOrDefault(drift.VariantId, string.Empty),
                drift.LocationId,
                codes.GetValueOrDefault(drift.LocationId, string.Empty),
                drift.RecordedOnHand,
                drift.LedgerOnHand,
                drift.RecordedReserved,
                drift.HeldReserved));
        }
    }
}
