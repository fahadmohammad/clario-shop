using ClarioShop.Ordering.Contracts;
using ClarioShop.Ordering.Domain;
using ClarioShop.Ordering.Persistence;

namespace ClarioShop.Ordering.Services;

internal sealed class OrderQueries(OrderingDbContext db) : IOrderQueries
{
    public async Task<OrderPaymentInfo?> GetForPaymentAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new { o.Id, o.Number, o.CustomerId, o.Status, o.PaymentMethod, o.Total })
            .FirstOrDefaultAsync(cancellationToken);

        return order is null
            ? null
            : new OrderPaymentInfo(order.Id, OrderNumber.Format(order.Number), order.CustomerId, order.Status.ToString(), order.PaymentMethod.ToString(), order.Total);
    }
}
