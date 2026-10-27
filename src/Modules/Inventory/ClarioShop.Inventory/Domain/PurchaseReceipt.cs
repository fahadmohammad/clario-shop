using ClarioShop.SharedKernel.Domain;
using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Inventory.Domain;

/// <summary>Goods received from a supplier into one location. Each line becomes a PurchaseReceipt movement.</summary>
internal sealed class PurchaseReceipt : Entity
{
    private readonly List<PurchaseReceiptLine> lines = [];

    private PurchaseReceipt()
    {
    }

    public Guid LocationId { get; private set; }

    public string? SupplierName { get; private set; }

    /// <summary>The supplier's invoice or challan number.</summary>
    public string? SupplierReference { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    public Guid? RecordedBy { get; private set; }

    public IReadOnlyCollection<PurchaseReceiptLine> Lines => lines;

    public decimal TotalCost => lines.Sum(line => line.Quantity * line.UnitCost);

    public static Result<PurchaseReceipt> Create(
        Guid locationId,
        string? supplierName,
        string? supplierReference,
        string? note,
        IReadOnlyList<(Guid VariantId, decimal Quantity, decimal UnitCost)> received,
        DateTimeOffset receivedAt,
        DateTimeOffset recordedAt,
        Guid? recordedBy)
    {
        if (received.Count == 0)
        {
            return Error.Validation("lines", "A receipt needs at least one line.");
        }

        if (received.Any(line => line.Quantity <= 0))
        {
            return Error.Validation("lines", "Every received quantity must be above zero.");
        }

        if (received.Any(line => line.UnitCost < 0))
        {
            return Error.Validation("lines", "Unit cost cannot be negative.");
        }

        if (received.GroupBy(line => line.VariantId).FirstOrDefault(group => group.Count() > 1) is { } duplicate)
        {
            return Error.Validation("lines", $"Variant {duplicate.Key} appears on more than one line.");
        }

        if (receivedAt > recordedAt)
        {
            return Error.Validation("receivedAt", "Goods cannot be received in the future.");
        }

        var receipt = new PurchaseReceipt
        {
            Id = Guid.CreateVersion7(),
            LocationId = locationId,
            SupplierName = Clean(supplierName),
            SupplierReference = Clean(supplierReference),
            Note = Clean(note),
            ReceivedAt = receivedAt,
            RecordedAt = recordedAt,
            RecordedBy = recordedBy,
        };

        receipt.lines.AddRange(received.Select(line => PurchaseReceiptLine.Create(receipt.Id, line.VariantId, line.Quantity, line.UnitCost)));
        return receipt;
    }

    /// <summary>The ledger entries this receipt adds, one per line.</summary>
    public IReadOnlyList<StockMovement> ToMovements() =>
        lines.Select(line => StockMovement.Record(
                line.VariantId,
                LocationId,
                MovementType.PurchaseReceipt,
                line.Quantity,
                StockReference.PurchaseReceipt(Id),
                ReceivedAt,
                RecordedAt,
                RecordedBy,
                line.UnitCost).Value)
            .ToList();

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
