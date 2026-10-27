using System.Runtime.CompilerServices;
using ClarioShop.Inventory.Domain;
using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Inventory.Tests.Domain;

public sealed class StockMovementTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("PurchaseReceipt", 5)]
    [InlineData("CustomerReturn", 1)]
    [InlineData("TransferIn", 2)]
    [InlineData("OnlineSale", -1)]
    [InlineData("PosSale", -0.250)]
    [InlineData("TransferOut", -2)]
    public void Record_QuantityMatchesTypeSign_Succeeds(string type, decimal quantity)
    {
        var result = Record(Enum.Parse<MovementType>(type), quantity);

        Assert.True(result.IsSuccess);
        Assert.Equal(quantity, result.Value.Quantity);
    }

    [Theory]
    [InlineData("PurchaseReceipt", -5)]
    [InlineData("PurchaseReceipt", 0)]
    [InlineData("OnlineSale", 1)]
    [InlineData("PosSale", 0)]
    [InlineData("TransferIn", -1)]
    public void Record_QuantityAgainstTypeSign_ReturnsValidationError(string type, decimal quantity)
    {
        var result = Record(Enum.Parse<MovementType>(type), quantity);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Record_AdjustmentWithoutReason_ReturnsValidationError()
    {
        var result = Record(MovementType.Adjustment, -1);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Record_ReceiptWithReason_ReturnsValidationError()
    {
        var result = Record(MovementType.PurchaseReceipt, 1, AdjustmentReason.Other);

        Assert.True(result.IsFailure);
    }

    [Theory]
    [InlineData("Damaged")]
    [InlineData("Expired")]
    [InlineData("Theft")]
    public void Record_WriteOffReasonIncreasingStock_ReturnsValidationError(string reason)
    {
        var result = Record(MovementType.Adjustment, 3, Enum.Parse<AdjustmentReason>(reason));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(-3)]
    public void Record_CountCorrection_AllowsEitherDirection(decimal quantity)
    {
        var result = Record(MovementType.Adjustment, quantity, AdjustmentReason.CountCorrection);

        Assert.True(result.IsSuccess);
        Assert.Equal(AdjustmentReason.CountCorrection, result.Value.Reason);
    }

    [Fact]
    public void StockMovement_HasNoMutableProperties()
    {
        var mutable = typeof(StockMovement).GetProperties()
            .Where(property => property.SetMethod is { } setter
                && !setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)))
            .Select(property => property.Name)
            .ToList();

        Assert.Empty(mutable);
    }

    private static Result<StockMovement> Record(MovementType type, decimal quantity, AdjustmentReason? reason = null) =>
        StockMovement.Record(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            type,
            quantity,
            new StockReference("Test", Guid.CreateVersion7()),
            Now,
            Now,
            userId: null,
            reason: reason);
}
