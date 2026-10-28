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

    /// <summary>An order with its lines and history, tracked for a status change.</summary>
    public static Task<Order?> LoadOrderAsync(this OrderingDbContext db, Guid orderId, CancellationToken cancellationToken) =>
        db.Orders
            .Include(order => order.Lines)
            .Include(order => order.History)
            .AsSplitQuery()
            .FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);

    /// <summary>Saves a status change; a concurrent change to the same order (RowVersion) is a conflict, not a lost update.</summary>
    public static async Task<Result<OrderDetails>> SaveOrderAsync(this OrderingDbContext db, Order order, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OrderingErrors.OrderChanged;
        }

        return OrderDetails.From(order);
    }
}
