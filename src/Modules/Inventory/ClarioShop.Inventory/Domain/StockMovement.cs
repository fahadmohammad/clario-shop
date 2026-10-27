using ClarioShop.SharedKernel.Domain;
using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Inventory.Domain;

/// <summary>
/// One line of the append-only stock ledger. Never updated or deleted (the database refuses it too);
/// a mistake is corrected by a new movement.
/// </summary>
internal sealed class StockMovement : Entity
{
    private StockMovement()
    {
    }

    public Guid VariantId { get; private init; }

    public Guid LocationId { get; private init; }

    /// <summary>Signed: positive adds stock, negative removes it. Decimal for items sold by weight.</summary>
    public decimal Quantity { get; private init; }

    public MovementType Type { get; private init; }

    /// <summary>Set for <see cref="MovementType.Adjustment"/> only.</summary>
    public AdjustmentReason? Reason { get; private init; }

    /// <summary>What caused the movement, e.g. "PurchaseReceipt", "StockAdjustment", "Order".</summary>
    public string ReferenceType { get; private init; } = string.Empty;

    public Guid ReferenceId { get; private init; }

    /// <summary>When it happened in the shop (a receipt may be keyed in after the goods arrived).</summary>
    public DateTimeOffset OccurredAt { get; private init; }

    /// <summary>When the server wrote it.</summary>
    public DateTimeOffset RecordedAt { get; private init; }

    public Guid? UserId { get; private init; }

    public decimal? UnitCost { get; private init; }

    public static Result<StockMovement> Record(
        Guid variantId,
        Guid locationId,
        MovementType type,
        decimal quantity,
        StockReference reference,
        DateTimeOffset occurredAt,
        DateTimeOffset recordedAt,
        Guid? userId,
        decimal? unitCost = null,
        AdjustmentReason? reason = null)
    {
        if (!HasValidSign(type, quantity))
        {
            return Error.Validation("quantity", $"A {type} movement cannot have quantity {quantity:0.###}.");
        }

        if ((type == MovementType.Adjustment) != reason.HasValue)
        {
            return Error.Validation("reason", "Adjustments, and only adjustments, carry a reason code.");
        }

        if (reason is { } code && !AllowsQuantity(code, quantity))
        {
            return Error.Validation("reason", $"A {code} adjustment can only reduce stock.");
        }

        if (unitCost is < 0)
        {
            return Error.Validation("unitCost", "Unit cost cannot be negative.");
        }

        return new StockMovement
        {
            Id = Guid.CreateVersion7(),
            VariantId = variantId,
            LocationId = locationId,
            Type = type,
            Quantity = quantity,
            Reason = reason,
            ReferenceType = reference.Type,
            ReferenceId = reference.Id,
            OccurredAt = occurredAt,
            RecordedAt = recordedAt,
            UserId = userId,
            UnitCost = unitCost is { } cost ? decimal.Round(cost, 2) : null,
        };
    }

    public static bool HasValidSign(MovementType type, decimal quantity) => type switch
    {
        MovementType.PurchaseReceipt or MovementType.CustomerReturn or MovementType.TransferIn => quantity > 0,
        MovementType.OnlineSale or MovementType.PosSale or MovementType.TransferOut => quantity < 0,
        MovementType.Adjustment => quantity != 0,
        _ => false,
    };

    public static bool AllowsQuantity(AdjustmentReason reason, decimal quantity) => reason switch
    {
        AdjustmentReason.Damaged or AdjustmentReason.Expired or AdjustmentReason.Theft => quantity < 0,
        _ => quantity != 0,
    };
}

