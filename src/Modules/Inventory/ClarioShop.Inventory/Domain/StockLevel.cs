using ClarioShop.SharedKernel.Domain;
using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Inventory.Domain;

/// <summary>
/// The current stock of one variant at one location: a projection of the movement ledger, updated in the
/// same transaction as each movement. <see cref="RowVersion"/> guards against lost updates.
/// </summary>
internal sealed class StockLevel : Entity
{
    private StockLevel()
    {
    }

    public Guid VariantId { get; private set; }

    public Guid LocationId { get; private set; }

    /// <summary>Physically in the location. May go negative when a POS sale outruns the system.</summary>
    public decimal OnHand { get; private set; }

    /// <summary>Held for online orders awaiting payment.</summary>
    public decimal Reserved { get; private set; }

    public decimal Available => OnHand - Reserved;

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Concurrency token (PostgreSQL xmin).</summary>
    public uint RowVersion { get; private set; }

    public static StockLevel Create(Guid variantId, Guid locationId, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        VariantId = variantId,
        LocationId = locationId,
        UpdatedAt = now,
    };

    /// <summary>Applies a movement unconditionally (sales and returns are facts, even if stock goes negative).</summary>
    public void Apply(StockMovement movement)
    {
        EnsureSameItem(movement);
        OnHand += movement.Quantity;
        UpdatedAt = movement.RecordedAt;
    }

    /// <summary>Applies a staff adjustment, which may not take on-hand stock below zero.</summary>
    public Result ApplyAdjustment(StockMovement movement)
    {
        EnsureSameItem(movement);
        if (movement.Quantity < 0 && OnHand + movement.Quantity < 0)
        {
            return InventoryErrors.InsufficientStock(VariantId, OnHand, -movement.Quantity);
        }

        Apply(movement);
        return Result.Success();
    }

    public Result Reserve(decimal quantity, DateTimeOffset now)
    {
        if (quantity <= 0)
        {
            return Error.Validation("quantity", "A reservation needs a quantity above zero.");
        }

        if (Available < quantity)
        {
            return InventoryErrors.InsufficientStock(VariantId, Math.Max(Available, 0), quantity);
        }

        Reserved += quantity;
        UpdatedAt = now;
        return Result.Success();
    }

    public void Release(decimal quantity, DateTimeOffset now)
    {
        Reserved = Math.Max(0, Reserved - quantity);
        UpdatedAt = now;
    }

    /// <summary>Turns reserved stock into a sale: the hold is dropped and the (negative) sale movement applied.</summary>
    public void CommitReservation(StockMovement sale)
    {
        Release(-sale.Quantity, sale.RecordedAt);
        Apply(sale);
    }

    private void EnsureSameItem(StockMovement movement)
    {
        if (movement.VariantId != VariantId || movement.LocationId != LocationId)
        {
            throw new InvalidOperationException("A movement can only be applied to the stock level of its own variant and location.");
        }
    }
}
