using ClarioShop.SharedKernel.Domain;

namespace ClarioShop.Inventory.Domain;

/// <summary>
/// A stock level the drift check found out of step with the ledger, kept as evidence after the level was set back
/// to the ledger's figures.
/// </summary>
internal sealed class StockDrift : Entity
{
    private StockDrift()
    {
    }

    /// <summary>The drift check run that found it; one run finds every drift at once.</summary>
    public Guid CheckId { get; private init; }

    public Guid VariantId { get; private init; }

    public Guid LocationId { get; private init; }

    public decimal RecordedOnHand { get; private init; }

    public decimal LedgerOnHand { get; private init; }

    public decimal RecordedReserved { get; private init; }

    public decimal HeldReserved { get; private init; }

    public DateTimeOffset DetectedAt { get; private init; }

    public static StockDrift Record(Guid checkId, StockLevel level, decimal ledgerOnHand, decimal heldReserved, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        CheckId = checkId,
        VariantId = level.VariantId,
        LocationId = level.LocationId,
        RecordedOnHand = level.OnHand,
        LedgerOnHand = ledgerOnHand,
        RecordedReserved = level.Reserved,
        HeldReserved = heldReserved,
        DetectedAt = now,
    };
}
