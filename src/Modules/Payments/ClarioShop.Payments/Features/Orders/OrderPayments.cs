using ClarioShop.Ordering.Contracts;
using ClarioShop.Payments.Domain;

namespace ClarioShop.Payments.Features.Orders;

/// <summary>An order's payments and what is still owed on it.</summary>
/// <param name="PaymentStatus">Unpaid, PartiallyPaid or Paid.</param>
public sealed record OrderPayments(
    Guid OrderId,
    string OrderNumber,
    decimal Total,
    decimal AmountPaid,
    decimal Outstanding,
    string PaymentStatus,
    IReadOnlyList<PaymentItem> Payments)
{
    internal static OrderPayments From(OrderPaymentInfo order, IReadOnlyCollection<Payment> payments)
    {
        var balance = OrderBalance.Of(order.Total, payments);
        return new OrderPayments(
            order.OrderId,
            order.Number,
            order.Total,
            balance.Paid,
            balance.Outstanding,
            balance.Status.ToString(),
            [.. payments.OrderBy(payment => payment.CreatedAt).ThenBy(payment => payment.Id).Select(PaymentItem.From)]);
    }
}

public sealed record PaymentItem(
    Guid Id,
    string Method,
    string Status,
    decimal Amount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<PaymentAttemptItem> Attempts)
{
    internal static PaymentItem From(Payment payment) => new(
        payment.Id,
        payment.Method.ToString(),
        payment.Status.ToString(),
        payment.Amount,
        payment.CreatedAt,
        payment.CompletedAt,
        [.. payment.Attempts.OrderBy(attempt => attempt.AttemptedAt).ThenBy(attempt => attempt.Id).Select(PaymentAttemptItem.From)]);
}

public sealed record PaymentAttemptItem(string Outcome, DateTimeOffset AttemptedAt, Guid? RecordedByUserId, string? Note)
{
    internal static PaymentAttemptItem From(PaymentAttempt attempt) =>
        new(attempt.Outcome.ToString(), attempt.AttemptedAt, attempt.RecordedByUserId, attempt.Note);
}
