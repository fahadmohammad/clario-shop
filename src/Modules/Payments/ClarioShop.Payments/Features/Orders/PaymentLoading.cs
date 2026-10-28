using ClarioShop.Payments.Domain;
using ClarioShop.Payments.Persistence;

namespace ClarioShop.Payments.Features.Orders;

internal static class PaymentLoading
{
    /// <summary>Every payment for the order with its attempts.</summary>
    public static Task<List<Payment>> PaymentsForOrderAsync(this PaymentsDbContext db, Guid orderId, CancellationToken cancellationToken) =>
        db.Payments.AsNoTracking()
            .Include(payment => payment.Attempts)
            .Where(payment => payment.OrderId == orderId)
            .ToListAsync(cancellationToken);
}
