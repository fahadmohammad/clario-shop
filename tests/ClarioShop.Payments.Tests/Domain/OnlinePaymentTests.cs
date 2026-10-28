using ClarioShop.Payments.Domain;

namespace ClarioShop.Payments.Tests.Domain;

public sealed class OnlinePaymentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void StartOnline_ValidAmount_IsPendingWithoutAttempts()
    {
        var payment = Start();

        Assert.Equal((PaymentMethod.SslCommerz, PaymentStatus.Pending, 900m, (DateTimeOffset?)null), (payment.Method, payment.Status, payment.Amount, payment.CompletedAt));
        Assert.Empty(payment.Attempts);
    }

    [Fact]
    public void StartOnline_CashOnDelivery_Throws() =>
        Assert.Throws<ArgumentException>(() => Payment.StartOnline(Guid.CreateVersion7(), "CS-000001", PaymentMethod.CashOnDelivery, 900m, Now));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void StartOnline_ZeroOrNegativeAmount_Throws(decimal amount) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Payment.StartOnline(Guid.CreateVersion7(), "CS-000001", PaymentMethod.SslCommerz, amount, Now));

    [Fact]
    public void StartAttempt_Pending_AddsPendingAttempt()
    {
        var payment = Start();

        var attempt = payment.StartAttempt(Now);

        Assert.Equal((payment.Id, AttemptOutcome.Pending, (DateTimeOffset?)null), (attempt.PaymentId, attempt.Outcome, attempt.CompletedAt));
        Assert.Same(attempt, Assert.Single(payment.Attempts));
    }

    [Fact]
    public void Settle_ConfirmedForTheAmount_SucceedsPaymentAndAttempt()
    {
        var payment = Start();
        var attempt = payment.StartAttempt(Now);

        var settled = payment.Settle(attempt.Id, Receipt(900m), Now.AddMinutes(3));

        Assert.Equal(SettleOutcome.Settled, settled.Value);
        Assert.Equal((PaymentStatus.Succeeded, (DateTimeOffset?)Now.AddMinutes(3)), (payment.Status, payment.CompletedAt));
        Assert.Equal((AttemptOutcome.Succeeded, "VAL-1", "BANK-1", "BKASH-BKash"), (attempt.Outcome, attempt.ValidationId, attempt.BankTransactionId, attempt.CardType));
    }

    [Fact]
    public void Settle_SameAttemptAgain_ReportsAlreadySettled()
    {
        var payment = Start();
        var attempt = payment.StartAttempt(Now);
        payment.Settle(attempt.Id, Receipt(900m), Now);

        var again = payment.Settle(attempt.Id, Receipt(900m), Now.AddMinutes(1));

        Assert.Equal(SettleOutcome.AlreadySettled, again.Value);
        Assert.Equal((DateTimeOffset?)Now, payment.CompletedAt);
    }

    [Fact]
    public void Settle_SecondAttemptAfterPaymentSucceeded_RecordsItAsPaidTwice()
    {
        var payment = Start();
        var first = payment.StartAttempt(Now);
        var second = payment.StartAttempt(Now);
        payment.Settle(first.Id, Receipt(900m), Now);

        var twice = payment.Settle(second.Id, Receipt(900m) with { ValidationId = "VAL-2" }, Now.AddMinutes(1));

        Assert.Equal(SettleOutcome.PaidTwice, twice.Value);
        Assert.Equal(AttemptOutcome.Succeeded, second.Outcome);
        Assert.Contains("refund", second.Note);
    }

    [Theory]
    [InlineData(899.99, "BDT")]
    [InlineData(900.01, "BDT")]
    [InlineData(900, "USD")]
    public void Settle_DifferentAmountOrCurrency_FailsAndChangesNothing(decimal amount, string currency)
    {
        var payment = Start();
        var attempt = payment.StartAttempt(Now);

        var settled = payment.Settle(attempt.Id, Receipt(amount) with { Currency = currency }, Now);

        Assert.Equal("payments.amount_mismatch", settled.Error.Code);
        Assert.Equal((PaymentStatus.Pending, AttemptOutcome.Pending), (payment.Status, attempt.Outcome));
    }

    [Fact]
    public void Settle_FlaggedRisky_IsHeldForReview()
    {
        var payment = Start();
        var attempt = payment.StartAttempt(Now);

        var settled = payment.Settle(attempt.Id, Receipt(900m) with { IsRisky = true }, Now);

        Assert.Equal("payments.held_for_review", settled.Error.Code);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    [Fact]
    public void FailAttempt_KeepsPaymentPendingForAnotherTry()
    {
        var payment = Start();
        var attempt = payment.StartAttempt(Now);

        payment.FailAttempt(attempt.Id, "payments.gateway_refused", Now);
        var retry = payment.StartAttempt(Now.AddMinutes(1));

        Assert.Equal((AttemptOutcome.Failed, "payments.gateway_refused", (DateTimeOffset?)Now), (attempt.Outcome, attempt.Note, attempt.CompletedAt));
        Assert.Equal((PaymentStatus.Pending, AttemptOutcome.Pending), (payment.Status, retry.Outcome));
    }

    [Fact]
    public void StartAttempt_PaymentSucceeded_Throws()
    {
        var payment = Start();
        payment.Settle(payment.StartAttempt(Now).Id, Receipt(900m), Now);

        Assert.Throws<InvalidOperationException>(() => payment.StartAttempt(Now));
    }

    [Fact]
    public void Fail_Pending_FailsPaymentAndOpenAttemptsButKeepsEarlierReasons()
    {
        var payment = Start();
        var cancelled = payment.StartAttempt(Now);
        var open = payment.StartAttempt(Now.AddMinutes(2));
        payment.FailAttempt(cancelled.Id, "SslCommerz reports CANCELLED.", Now.AddMinutes(40));

        payment.Fail("No payment confirmed.", Now.AddMinutes(40));

        Assert.Equal((PaymentStatus.Failed, (DateTimeOffset?)Now.AddMinutes(40)), (payment.Status, payment.CompletedAt));
        Assert.Equal((AttemptOutcome.Failed, "SslCommerz reports CANCELLED."), (cancelled.Outcome, cancelled.Note));
        Assert.Equal((AttemptOutcome.Failed, "No payment confirmed."), (open.Outcome, open.Note));
    }

    [Fact]
    public void Fail_AlreadySucceeded_Throws()
    {
        var payment = Start();
        var attempt = payment.StartAttempt(Now);
        payment.Settle(attempt.Id, Receipt(900m), Now);

        Assert.Throws<InvalidOperationException>(() => payment.Fail("No payment confirmed.", Now));
    }

    [Fact]
    public void Settle_AfterPaymentFailed_SucceedsAfterAll()
    {
        var payment = Start();
        var attempt = payment.StartAttempt(Now);
        payment.Fail("No payment confirmed.", Now.AddMinutes(40));

        var settled = payment.Settle(attempt.Id, Receipt(900m), Now.AddMinutes(41));

        Assert.Equal(SettleOutcome.Settled, settled.Value);
        Assert.Equal((PaymentStatus.Succeeded, AttemptOutcome.Succeeded), (payment.Status, attempt.Outcome));
    }

    private static Payment Start() => Payment.StartOnline(Guid.CreateVersion7(), "CS-000001", PaymentMethod.SslCommerz, 900m, Now);

    private static GatewayReceipt Receipt(decimal amount) => new("VAL-1", amount, "BDT", "BANK-1", "BKASH-BKash", IsRisky: false);
}
