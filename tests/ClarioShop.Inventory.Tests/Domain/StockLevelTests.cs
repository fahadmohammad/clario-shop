using ClarioShop.Inventory.Domain;
using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Inventory.Tests.Domain;

public sealed class StockLevelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid VariantId = Guid.CreateVersion7();
    private static readonly Guid LocationId = Guid.CreateVersion7();

    [Fact]
    public void Apply_Receipt_IncreasesOnHandAndAvailable()
    {
        var level = StockLevel.Create(VariantId, LocationId, Now);

        level.Apply(Movement(MovementType.PurchaseReceipt, 10));

        Assert.Equal(10, level.OnHand);
        Assert.Equal(10, level.Available);
    }

    [Fact]
    public void Apply_PosSaleBeyondStock_GoesNegative()
    {
        var level = WithStock(1);

        level.Apply(Movement(MovementType.PosSale, -3));

        Assert.Equal(-2, level.OnHand);
    }

    [Fact]
    public void Apply_MovementOfAnotherVariant_Throws()
    {
        var level = WithStock(1);
        var other = StockMovement.Record(Guid.CreateVersion7(), LocationId, MovementType.PurchaseReceipt, 1, new StockReference("Test", Guid.Empty), Now, Now, null).Value;

        Assert.Throws<InvalidOperationException>(() => level.Apply(other));
    }

    [Fact]
    public void Reserve_WithinAvailable_HoldsStock()
    {
        var level = WithStock(5);

        var result = level.Reserve(3, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, level.OnHand);
        Assert.Equal(3, level.Reserved);
        Assert.Equal(2, level.Available);
    }

    [Fact]
    public void Reserve_MoreThanAvailable_ReturnsInsufficientStock()
    {
        var level = WithStock(5);
        level.Reserve(4, Now);

        var result = level.Reserve(2, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("inventory.insufficient_stock", result.Error.Code);
        Assert.Equal(4, level.Reserved);
    }

    [Fact]
    public void Reserve_ZeroQuantity_ReturnsValidationError()
    {
        var level = WithStock(5);

        var result = level.Reserve(0, Now);

        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Release_MoreThanReserved_StopsAtZero()
    {
        var level = WithStock(5);
        level.Reserve(2, Now);

        level.Release(3, Now);

        Assert.Equal(0, level.Reserved);
        Assert.Equal(5, level.Available);
    }

    [Fact]
    public void CommitReservation_DropsHoldAndReducesOnHand()
    {
        var level = WithStock(5);
        level.Reserve(2, Now);

        level.CommitReservation(Movement(MovementType.OnlineSale, -2));

        Assert.Equal(3, level.OnHand);
        Assert.Equal(0, level.Reserved);
        Assert.Equal(3, level.Available);
    }

    [Fact]
    public void ApplyAdjustment_BelowZero_ReturnsInsufficientStock()
    {
        var level = WithStock(2);

        var result = level.ApplyAdjustment(Movement(MovementType.Adjustment, -3, AdjustmentReason.Damaged));

        Assert.Equal("inventory.insufficient_stock", result.Error.Code);
        Assert.Equal(2, level.OnHand);
    }

    [Fact]
    public void ApplyAdjustment_IncreaseWhileNegative_Succeeds()
    {
        var level = WithStock(0);
        level.Apply(Movement(MovementType.PosSale, -2));

        var result = level.ApplyAdjustment(Movement(MovementType.Adjustment, 5, AdjustmentReason.CountCorrection));

        Assert.True(result.IsSuccess);
        Assert.Equal(3, level.OnHand);
    }

    [Fact]
    public void DriftsFrom_MatchingLedgerAndHolds_IsFalse()
    {
        var level = WithStock(4);
        level.Reserve(1, Now);

        Assert.False(level.DriftsFrom(ledgerOnHand: 4, heldReserved: 1));
    }

    [Theory]
    [InlineData(5, 1)]
    [InlineData(4, 0)]
    public void DriftsFrom_OnHandOrReservedDiffers_IsTrue(decimal ledgerOnHand, decimal heldReserved)
    {
        var level = WithStock(4);
        level.Reserve(1, Now);

        Assert.True(level.DriftsFrom(ledgerOnHand, heldReserved));
    }

    [Fact]
    public void Correct_SetsOnHandAndReservedToTheLedger()
    {
        var level = WithStock(4);
        level.Reserve(1, Now);

        level.Correct(ledgerOnHand: 7, heldReserved: 2, Now.AddDays(1));

        Assert.Equal((7m, 2m, 5m, Now.AddDays(1)), (level.OnHand, level.Reserved, level.Available, level.UpdatedAt));
    }

    [Fact]
    public void ApplyTransferOut_WithinAvailable_ReducesOnHand()
    {
        var level = WithStock(10);
        level.Reserve(4, Now);

        var result = level.ApplyTransferOut(Movement(MovementType.TransferOut, -6));

        Assert.True(result.IsSuccess);
        Assert.Equal((4m, 4m, 0m), (level.OnHand, level.Reserved, level.Available));
    }

    [Fact]
    public void ApplyTransferOut_ReservedStock_ReturnsConflictAndLeavesLevel()
    {
        var level = WithStock(10);
        level.Reserve(4, Now);

        var result = level.ApplyTransferOut(Movement(MovementType.TransferOut, -7));

        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal((10m, 6m), (level.OnHand, level.Available));
    }

    [Fact]
    public void ApplyTransferOut_OtherMovementType_Throws()
    {
        var level = WithStock(10);

        Assert.Throws<InvalidOperationException>(() => level.ApplyTransferOut(Movement(MovementType.PosSale, -1)));
    }

    private static StockLevel WithStock(decimal onHand)
    {
        var level = StockLevel.Create(VariantId, LocationId, Now);
        if (onHand > 0)
        {
            level.Apply(Movement(MovementType.PurchaseReceipt, onHand));
        }

        return level;
    }

    private static StockMovement Movement(MovementType type, decimal quantity, AdjustmentReason? reason = null) =>
        StockMovement.Record(VariantId, LocationId, type, quantity, new StockReference("Test", Guid.Empty), Now, Now, null, reason: reason).Value;
}
