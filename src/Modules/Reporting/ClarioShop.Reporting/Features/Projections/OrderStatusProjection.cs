using ClarioShop.Reporting.Domain;
using ClarioShop.Reporting.Services;

namespace ClarioShop.Reporting.Features.Projections;

internal static class OrderStatusProjection
{
    /// <summary>Moves Reporting's copy of the order to <paramref name="status"/>, unless it already shows a newer change.</summary>
    public static Task MoveToAsync(
        this SalesOrderProjection projection,
        Guid orderId,
        string orderNumber,
        SalesOrderStatus status,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        projection.ApplyAsync(
            orderId,
            () => SalesOrder.Start(orderId, orderNumber, status, at),
            order => order.MoveTo(status, at),
            cancellationToken);
}
