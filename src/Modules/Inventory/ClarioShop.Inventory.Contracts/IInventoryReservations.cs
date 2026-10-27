using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Inventory.Contracts;

/// <summary>
/// Synchronous stock reservation for checkout. A reservation holds stock for
/// <see cref="ReservationLifetime"/>; payment success commits it into a sale, failure or expiry releases it.
/// </summary>
public interface IInventoryReservations
{
    /// <summary>How long a reservation holds stock before it expires.</summary>
    static readonly TimeSpan ReservationLifetime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Reserves every line or none. Calling it again for the same order while its reservation is active
    /// returns that reservation. Fails with <c>inventory.insufficient_stock</c> (conflict) when a line cannot be covered.
    /// </summary>
    Task<Result<StockReservationInfo>> ReserveAsync(ReserveStockRequest request, CancellationToken cancellationToken);

    /// <summary>Turns an active reservation into sale movements. Committing twice is a no-op.</summary>
    Task<Result> CommitAsync(Guid reservationId, CancellationToken cancellationToken);

    /// <summary>Gives the reserved stock back. Releasing an already released or expired reservation is a no-op.</summary>
    Task<Result> ReleaseAsync(Guid reservationId, CancellationToken cancellationToken);
}

/// <param name="OrderId">The order the stock is held for; one active reservation per order.</param>
/// <param name="LocationId">The location to reserve at; the tenant's default location when null.</param>
public sealed record ReserveStockRequest(Guid OrderId, IReadOnlyList<ReservationLine> Lines, Guid? LocationId = null);

/// <param name="Quantity">Units, or kg for items sold by weight.</param>
public sealed record ReservationLine(Guid VariantId, decimal Quantity);

public sealed record StockReservationInfo(
    Guid ReservationId,
    Guid OrderId,
    Guid LocationId,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<ReservationLine> Lines);
