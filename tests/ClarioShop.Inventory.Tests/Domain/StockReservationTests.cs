using ClarioShop.Inventory.Domain;
using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Inventory.Tests.Domain;

public sealed class StockReservationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 10, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);
    private static readonly Guid Rice = Guid.CreateVersion7();
    private static readonly Guid Oil = Guid.CreateVersion7();

    [Fact]
    public void Create_ValidLines_IsActiveForFifteenMinutes()
    {
        var reservation = Create((Rice, 2));

        Assert.Equal(ReservationStatus.Active, reservation.Status);
        Assert.Equal(Now.AddMinutes(15), reservation.ExpiresAt);
    }

    [Fact]
    public void Create_SameVariantTwice_MergesLines()
    {
        var reservation = Create((Rice, 2), (Oil, 1), (Rice, 3));

        Assert.Equal(2, reservation.Lines.Count);
        Assert.Equal(5, reservation.Lines.Single(line => line.VariantId == Rice).Quantity);
    }

    [Fact]
    public void Create_NoLines_ReturnsValidationError()
    {
        var result = StockReservation.Create(StockReference.Order(Guid.CreateVersion7()), Guid.CreateVersion7(), [], Lifetime, Now);

        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void Create_NonPositiveQuantity_ReturnsValidationError()
    {
        var result = StockReservation.Create(StockReference.Order(Guid.CreateVersion7()), Guid.CreateVersion7(), [(Rice, 0m)], Lifetime, Now);

        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public void IsDue_AtExpiry_IsTrue()
    {
        var reservation = Create((Rice, 1));

        Assert.False(reservation.IsDue(Now.AddMinutes(14).AddSeconds(59)));
        Assert.True(reservation.IsDue(Now.AddMinutes(15)));
    }

    [Fact]
    public void Commit_BeforeExpiry_MarksCommitted()
    {
        var reservation = Create((Rice, 1));

        var result = reservation.Commit(Now.AddMinutes(10));

        Assert.True(result.IsSuccess);
        Assert.Equal(ReservationStatus.Committed, reservation.Status);
        Assert.Equal(Now.AddMinutes(10), reservation.ClosedAt);
    }

    [Fact]
    public void Commit_AfterExpiry_ReturnsExpired()
    {
        var reservation = Create((Rice, 1));

        var result = reservation.Commit(Now.AddMinutes(16));

        Assert.Equal("inventory.reservation_expired", result.Error.Code);
        Assert.Equal(ReservationStatus.Active, reservation.Status);
    }

    [Fact]
    public void Commit_AfterRelease_ReturnsReleased()
    {
        var reservation = Create((Rice, 1));
        reservation.Release(Now);

        var result = reservation.Commit(Now);

        Assert.Equal("inventory.reservation_released", result.Error.Code);
    }

    [Fact]
    public void Release_Active_ReturnsTrueOnceThenFalse()
    {
        var reservation = Create((Rice, 1));

        var first = reservation.Release(Now);
        var second = reservation.Release(Now);

        Assert.True(first.Value);
        Assert.False(second.Value);
        Assert.Equal(ReservationStatus.Released, reservation.Status);
    }

    [Fact]
    public void Release_Committed_ReturnsConflict()
    {
        var reservation = Create((Rice, 1));
        reservation.Commit(Now);

        var result = reservation.Release(Now);

        Assert.Equal("inventory.reservation_committed", result.Error.Code);
    }

    [Fact]
    public void Expire_NotYetDue_DoesNothing()
    {
        var reservation = Create((Rice, 1));

        Assert.False(reservation.Expire(Now.AddMinutes(5)));
        Assert.Equal(ReservationStatus.Active, reservation.Status);
    }

    [Fact]
    public void Expire_Due_MarksExpired()
    {
        var reservation = Create((Rice, 1));

        Assert.True(reservation.Expire(Now.AddMinutes(15)));
        Assert.Equal(ReservationStatus.Expired, reservation.Status);
    }

    [Fact]
    public void CommitConfirmed_Active_CommitsAndReportsStockStillHeld()
    {
        var reservation = Create((Rice, 2));

        var result = reservation.CommitConfirmed(Now.AddMinutes(5));

        Assert.True(result.Value);
        Assert.Equal(ReservationStatus.Committed, reservation.Status);
    }

    [Fact]
    public void CommitConfirmed_DueButNotSwept_StillCommitsHeldStock()
    {
        var reservation = Create((Rice, 2));

        var result = reservation.CommitConfirmed(Now.AddMinutes(20));

        Assert.True(result.Value);
        Assert.Equal(ReservationStatus.Committed, reservation.Status);
    }

    [Fact]
    public void CommitConfirmed_Expired_CommitsAndReportsHoldAlreadyReturned()
    {
        var reservation = Create((Rice, 2));
        reservation.Expire(Now.AddMinutes(15));

        var result = reservation.CommitConfirmed(Now.AddMinutes(20));

        Assert.False(result.Value);
        Assert.Equal(ReservationStatus.Committed, reservation.Status);
    }

    [Fact]
    public void CommitConfirmed_Released_ReturnsReleasedError()
    {
        var reservation = Create((Rice, 2));
        reservation.Release(Now);

        var result = reservation.CommitConfirmed(Now);

        Assert.Equal(InventoryErrors.ReservationReleased, result.Error);
    }

    [Theory]
    [InlineData("Active", "ReturnHold", "Released")]
    [InlineData("Expired", "Nothing", "Released")]
    [InlineData("Released", "Nothing", "Released")]
    [InlineData("Committed", "ReverseSale", "Reversed")]
    public void Cancel_ByStatus_ReturnsWhatToDoWithTheStock(string before, string expected, string after)
    {
        var reservation = InStatus(Enum.Parse<ReservationStatus>(before));

        var outcome = reservation.Cancel(Now.AddMinutes(20));

        Assert.Equal((Enum.Parse<CancellationOutcome>(expected), Enum.Parse<ReservationStatus>(after)), (outcome, reservation.Status));
    }

    [Fact]
    public void Cancel_Twice_SecondTimeDoesNothing()
    {
        var reservation = InStatus(ReservationStatus.Committed);
        reservation.Cancel(Now);

        var second = reservation.Cancel(Now);

        Assert.Equal((CancellationOutcome.Nothing, ReservationStatus.Reversed), (second, reservation.Status));
    }

    [Fact]
    public void CommitConfirmed_AfterCancel_DoesNotSellAgain()
    {
        var reservation = InStatus(ReservationStatus.Expired);
        reservation.Cancel(Now.AddMinutes(20));

        var result = reservation.CommitConfirmed(Now.AddMinutes(21));

        Assert.Equal(InventoryErrors.ReservationReleased, result.Error);
    }

    private static StockReservation InStatus(ReservationStatus status)
    {
        var reservation = Create((Rice, 2));
        switch (status)
        {
            case ReservationStatus.Expired:
                reservation.Expire(Now.AddMinutes(15));
                break;
            case ReservationStatus.Released:
                reservation.Release(Now);
                break;
            case ReservationStatus.Committed:
                reservation.Commit(Now);
                break;
        }

        return reservation;
    }

    private static StockReservation Create(params (Guid VariantId, decimal Quantity)[] lines) =>
        StockReservation.Create(StockReference.Order(Guid.CreateVersion7()), Guid.CreateVersion7(), lines, Lifetime, Now).Value;
}
