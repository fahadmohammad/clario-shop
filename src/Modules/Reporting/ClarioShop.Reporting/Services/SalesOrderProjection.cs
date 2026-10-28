using ClarioShop.Reporting.Domain;
using ClarioShop.Reporting.Persistence;
using Npgsql;

namespace ClarioShop.Reporting.Services;

/// <summary>
/// Applies one order event to Reporting's copy of the order, creating the copy when the event is the first one heard
/// of. Two events of one order handled at once (two API instances) never lose a change: the later save sees the
/// row changed (RowVersion) or already created (primary key), and the event is applied again on the fresh row.
/// </summary>
internal sealed class SalesOrderProjection(ReportingDbContext db)
{
    private const int MaxAttempts = 8;

    /// <param name="start">The order as this event alone describes it.</param>
    /// <param name="apply">The event's change; it must be safe to apply to the order more than once.</param>
    public async Task ApplyAsync(Guid orderId, Func<SalesOrder> start, Action<SalesOrder> apply, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var order = await db.SalesOrders.Include(o => o.Lines).SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken);
            if (order is null)
            {
                order = start();
                db.SalesOrders.Add(order);
            }

            apply(order);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException exception) when (attempt < MaxAttempts && IsConflict(exception))
            {
                db.ChangeTracker.Clear();
                await Task.Delay(TimeSpan.FromMilliseconds(Random.Shared.Next(5, 20) * attempt), cancellationToken);
            }
        }
    }

    private static bool IsConflict(DbUpdateException exception) =>
        exception is DbUpdateConcurrencyException
        || exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
