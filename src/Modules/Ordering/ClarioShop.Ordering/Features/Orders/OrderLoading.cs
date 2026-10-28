using ClarioShop.Ordering.Domain;
using ClarioShop.Ordering.Persistence;

namespace ClarioShop.Ordering.Features.Orders;

internal static class OrderLoading
{
    /// <summary>An order with its lines and history, read-only.</summary>
    public static Task<Order?> FindOrderAsync(this OrderingDbContext db, Guid orderId, CancellationToken cancellationToken) =>
        db.Orders.AsNoTracking()
            .Include(order => order.Lines)
            .Include(order => order.History)
            .AsSplitQuery()
            .FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);
}
