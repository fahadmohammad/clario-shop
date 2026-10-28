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

    public async Task<OrderGatewayInfo?> GetForGatewayAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new
            {
                o.Id,
                o.Number,
                o.CustomerId,
                o.Status,
                o.PaymentMethod,
                o.Total,
                o.CustomerName,
                o.CustomerPhone,
                o.DeliveryAddress.Line1,
                o.DeliveryAddress.Line2,
                o.DeliveryAddress.Area,
                o.DeliveryAddress.City,
                o.DeliveryAddress.PostalCode,
                ItemCount = o.Lines.Count,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null)
        {
            return null;
        }

        return new OrderGatewayInfo(
            new OrderPaymentInfo(order.Id, OrderNumber.Format(order.Number), order.CustomerId, order.Status.ToString(), order.PaymentMethod.ToString(), order.Total),
            order.CustomerName,
            order.CustomerPhone,
            string.Join(", ", new[] { order.Line1, order.Line2, order.Area }.Where(part => !string.IsNullOrWhiteSpace(part))),
            order.City,
            order.PostalCode,
            order.ItemCount);
    }
}
