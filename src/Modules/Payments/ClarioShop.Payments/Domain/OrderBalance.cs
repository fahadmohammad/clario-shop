using ClarioShop.SharedKernel.Results;

namespace ClarioShop.Payments.Domain;

/// <summary>What an order costs against what its succeeded payments have brought in.</summary>
internal sealed record OrderBalance(decimal Total, decimal Paid)
{
    public decimal Outstanding => Math.Max(Total - Paid, 0m);

    public OrderPaymentStatus Status => Paid switch
    {
        <= 0m => OrderPaymentStatus.Unpaid,
        _ when Paid >= Total => OrderPaymentStatus.Paid,
        _ => OrderPaymentStatus.PartiallyPaid,
    };

    public static OrderBalance Of(decimal total, IEnumerable<Payment> payments) =>
        new(total, payments.Where(payment => payment.Status == PaymentStatus.Succeeded).Sum(payment => payment.Amount));

    /// <summary>A payment may cover what is still owed, never more: an order is never paid twice.</summary>
    public Result CanAccept(decimal amount) => amount > Outstanding
        ? PaymentsErrors.ExceedsOutstanding(amount, Outstanding)
        : Result.Success();
}
