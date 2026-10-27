using ClarioShop.SharedKernel.Domain;
using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Inventory.Domain;

/// <summary>A manual stock correction with a reason code (damage, expiry, theft, count correction …).</summary>
internal sealed class StockAdjustment : Entity
{
    private readonly List<StockAdjustmentLine> lines = [];

    private StockAdjustment()
    {
    }

    public Guid LocationId { get; private set; }

    public AdjustmentReason Reason { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    public Guid? RecordedBy { get; private set; }

    public IReadOnlyCollection<StockAdjustmentLine> Lines => lines;

    public static Result<StockAdjustment> Create(
        Guid locationId,
        AdjustmentReason reason,
        string? note,
        IReadOnlyList<(Guid VariantId, decimal QuantityChange)> changes,
        DateTimeOffset recordedAt,
        Guid? recordedBy)
    {
        if (changes.Count == 0)
        {
            return Error.Validation("lines", "An adjustment needs at least one line.");
        }

        if (changes.Any(line => !StockMovement.AllowsQuantity(reason, line.QuantityChange)))
        {
            return Error.Validation("lines", reason is AdjustmentReason.CountCorrection or AdjustmentReason.Other
                ? "A quantity change cannot be zero."
                : $"A {reason} adjustment can only reduce stock.");
        }

        if (changes.GroupBy(line => line.VariantId).FirstOrDefault(group => group.Count() > 1) is { } duplicate)
        {
            return Error.Validation("lines", $"Variant {duplicate.Key} appears on more than one line.");
        }

        if (reason == AdjustmentReason.Other && string.IsNullOrWhiteSpace(note))
        {
            return Error.Validation("note", "Say why when the reason is Other.");
        }

        var adjustment = new StockAdjustment
        {
            Id = Guid.CreateVersion7(),
            LocationId = locationId,
            Reason = reason,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            RecordedAt = recordedAt,
            RecordedBy = recordedBy,
        };

        adjustment.lines.AddRange(changes.Select(line => StockAdjustmentLine.Create(adjustment.Id, line.VariantId, line.QuantityChange)));
        return adjustment;
    }

    /// <summary>The ledger entries this adjustment adds, one per line.</summary>
    public IReadOnlyList<StockMovement> ToMovements() =>
        lines.Select(line => StockMovement.Record(
                line.VariantId,
                LocationId,
                MovementType.Adjustment,
                line.QuantityChange,
                StockReference.Adjustment(Id),
                RecordedAt,
                RecordedAt,
                RecordedBy,
                reason: Reason).Value)
            .ToList();
}
