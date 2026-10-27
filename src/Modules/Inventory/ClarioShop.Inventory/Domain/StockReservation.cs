using ClarioShop.SharedKernel.Domain;
using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Inventory.Domain;

/// <summary>Stock held for an online order during checkout. Committed on payment, released on failure or expiry.</summary>
internal sealed class StockReservation : Entity
{
    private readonly List<StockReservationLine> lines = [];

    private StockReservation()
    {
    }

    public string ReferenceType { get; private set; } = string.Empty;

    public Guid ReferenceId { get; private set; }

    public Guid LocationId { get; private set; }

    public ReservationStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public IReadOnlyCollection<StockReservationLine> Lines => lines;

    /// <summary>Creates an active reservation; quantities for the same variant are merged.</summary>
    public static Result<StockReservation> Create(
        StockReference reference,
        Guid locationId,
        IReadOnlyList<(Guid VariantId, decimal Quantity)> requested,
        TimeSpan lifetime,
        DateTimeOffset now)
    {
        if (requested.Count == 0)
        {
            return Error.Validation("lines", "A reservation needs at least one line.");
        }

        if (requested.Any(line => line.Quantity <= 0))
        {
            return Error.Validation("lines", "Every line needs a quantity above zero.");
        }

        var reservation = new StockReservation
        {
            Id = Guid.CreateVersion7(),
            ReferenceType = reference.Type,
            ReferenceId = reference.Id,
            LocationId = locationId,
            Status = ReservationStatus.Active,
            CreatedAt = now,
            ExpiresAt = now + lifetime,
        };

        reservation.lines.AddRange(requested
            .GroupBy(line => line.VariantId)
            .Select(group => StockReservationLine.Create(reservation.Id, group.Key, group.Sum(line => line.Quantity))));
        return reservation;
    }

    public bool IsDue(DateTimeOffset now) => Status == ReservationStatus.Active && now >= ExpiresAt;

    /// <summary>Marks the reservation as sold. The caller writes the sale movements.</summary>
    public Result Commit(DateTimeOffset now)
    {
        var closed = EnsureActive(now);
        if (closed.IsFailure)
        {
            return closed;
        }

        Close(ReservationStatus.Committed, now);
        return Result.Success();
    }

    /// <summary>
    /// Releases the hold. Returns true when stock must be given back now, false when it already was
    /// (released or expired before). Fails for a committed reservation.
    /// </summary>
    public Result<bool> Release(DateTimeOffset now)
    {
        switch (Status)
        {
            case ReservationStatus.Committed:
                return InventoryErrors.ReservationCommitted;
            case ReservationStatus.Released or ReservationStatus.Expired:
                return false;
            default:
                Close(ReservationStatus.Released, now);
                return true;
        }
    }

    /// <summary>Expires a reservation whose time is up. Returns false if it was not due.</summary>
    public bool Expire(DateTimeOffset now)
    {
        if (!IsDue(now))
        {
            return false;
        }

        Close(ReservationStatus.Expired, now);
        return true;
    }

    private Result EnsureActive(DateTimeOffset now) => Status switch
    {
        ReservationStatus.Committed => InventoryErrors.ReservationCommitted,
        ReservationStatus.Released => InventoryErrors.ReservationReleased,
        ReservationStatus.Expired => InventoryErrors.ReservationExpired,
        _ when IsDue(now) => InventoryErrors.ReservationExpired,
        _ => Result.Success(),
    };

    private void Close(ReservationStatus status, DateTimeOffset now)
    {
        Status = status;
        ClosedAt = now;
    }
}
