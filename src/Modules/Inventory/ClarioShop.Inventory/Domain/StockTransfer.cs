using ClarioShop.SharedKernel.Domain;
using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Inventory.Domain;

/// <summary>
/// Stock moved from one location to another: one document whose lines each become a <see cref="MovementType.TransferOut"/>
/// at the source and a <see cref="MovementType.TransferIn"/> at the destination, saved together.
/// </summary>
internal sealed class StockTransfer : Entity
{
    private readonly List<StockTransferLine> lines = [];

    private StockTransfer()
    {
    }

    public Guid FromLocationId { get; private set; }

    public Guid ToLocationId { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    public Guid? RecordedBy { get; private set; }

    public IReadOnlyCollection<StockTransferLine> Lines => lines;

    public static Result<StockTransfer> Create(
        Guid fromLocationId,
        Guid toLocationId,
        string? note,
        IReadOnlyList<(Guid VariantId, decimal Quantity)> quantities,
        DateTimeOffset recordedAt,
        Guid? recordedBy)
    {
        if (fromLocationId == toLocationId)
        {
            return Error.Validation("toLocationId", "Stock can only be transferred to a different location.");
        }

        if (quantities.Count == 0)
        {
            return Error.Validation("lines", "A transfer needs at least one line.");
        }

        if (quantities.Any(line => line.Quantity <= 0))
        {
            return Error.Validation("lines", "A transfer quantity must be above zero.");
        }

        if (quantities.GroupBy(line => line.VariantId).FirstOrDefault(group => group.Count() > 1) is { } duplicate)
        {
            return Error.Validation("lines", $"Variant {duplicate.Key} appears on more than one line.");
        }

        var transfer = new StockTransfer
        {
            Id = Guid.CreateVersion7(),
            FromLocationId = fromLocationId,
            ToLocationId = toLocationId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            RecordedAt = recordedAt,
            RecordedBy = recordedBy,
        };

        transfer.lines.AddRange(quantities.Select(line => StockTransferLine.Create(transfer.Id, line.VariantId, line.Quantity)));
        return transfer;
    }

    /// <summary>The ledger entries this transfer adds: per line, one leaving the source and one arriving at the destination.</summary>
    public IReadOnlyList<(StockMovement Out, StockMovement In)> ToMovements() =>
        lines.Select(line => (
                Record(line, FromLocationId, MovementType.TransferOut, -line.Quantity),
                Record(line, ToLocationId, MovementType.TransferIn, line.Quantity)))
            .ToList();

    private StockMovement Record(StockTransferLine line, Guid locationId, MovementType type, decimal quantity) =>
        StockMovement.Record(
            line.VariantId,
            locationId,
            type,
            quantity,
            StockReference.Transfer(Id),
            RecordedAt,
            RecordedAt,
            RecordedBy).Value;
}
