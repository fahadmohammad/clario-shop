using ClarioShop.Inventory.Domain;
using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Inventory.Tests.Domain;

public sealed class StockDocumentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid LocationId = Guid.CreateVersion7();
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly Guid Rice = Guid.CreateVersion7();
    private static readonly Guid Oil = Guid.CreateVersion7();

    [Fact]
    public void PurchaseReceipt_ToMovements_AddsOneReceiptMovementPerLineWithCost()
    {
        var receipt = PurchaseReceipt.Create(LocationId, "Pran Foods", "INV-1", null, [(Rice, 10m, 120m), (Oil, 4m, 175.5m)], Now.AddHours(-2), Now, UserId).Value;

        var movements = receipt.ToMovements();

        Assert.Equal(2, movements.Count);
        Assert.All(movements, m =>
        {
            Assert.Equal(MovementType.PurchaseReceipt, m.Type);
            Assert.Equal("PurchaseReceipt", m.ReferenceType);
            Assert.Equal(receipt.Id, m.ReferenceId);
            Assert.Equal(Now.AddHours(-2), m.OccurredAt);
            Assert.Equal(Now, m.RecordedAt);
            Assert.Equal(UserId, m.UserId);
        });
        Assert.Equal(175.5m, movements.Single(m => m.VariantId == Oil).UnitCost);
        Assert.Equal(1902m, receipt.TotalCost);
    }

    [Fact]
    public void PurchaseReceipt_ReceivedInFuture_ReturnsValidationError()
    {
        var result = PurchaseReceipt.Create(LocationId, null, null, null, [(Rice, 1m, 1m)], Now.AddMinutes(5), Now, UserId);

        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void PurchaseReceipt_DuplicateVariant_ReturnsValidationError()
    {
        var result = PurchaseReceipt.Create(LocationId, null, null, null, [(Rice, 1m, 1m), (Rice, 2m, 1m)], Now, Now, UserId);

        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void StockAdjustment_Damaged_CreatesNegativeAdjustmentMovements()
    {
        var adjustment = StockAdjustment.Create(LocationId, AdjustmentReason.Damaged, "Torn bags", [(Rice, -2m)], Now, UserId).Value;

        var movement = Assert.Single(adjustment.ToMovements());

        Assert.Equal(MovementType.Adjustment, movement.Type);
        Assert.Equal(AdjustmentReason.Damaged, movement.Reason);
        Assert.Equal(-2m, movement.Quantity);
        Assert.Equal("StockAdjustment", movement.ReferenceType);
    }

    [Fact]
    public void StockAdjustment_DamagedIncrease_ReturnsValidationError()
    {
        var result = StockAdjustment.Create(LocationId, AdjustmentReason.Damaged, null, [(Rice, 2m)], Now, UserId);

        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void StockAdjustment_OtherWithoutNote_ReturnsValidationError()
    {
        var result = StockAdjustment.Create(LocationId, AdjustmentReason.Other, " ", [(Rice, 1m)], Now, UserId);

        Assert.Equal("note", Assert.Single(result.Error.ValidationErrors!).Key);
    }

    [Fact]
    public void StockAdjustment_ZeroChange_ReturnsValidationError()
    {
        var result = StockAdjustment.Create(LocationId, AdjustmentReason.CountCorrection, null, [(Rice, 0m)], Now, UserId);

        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Location_CreateDefault_IsDefaultMainLocation()
    {
        var location = Location.CreateDefault(Now);

        Assert.True(location.IsDefault);
        Assert.Equal(Location.DefaultCode, location.Code);
    }
}
