using ClarioShop.Inventory.Contracts;
using ClarioShop.Ordering.Contracts.Events;
using ClarioShop.Ordering.Domain;
using ClarioShop.Ordering.Persistence;
using ClarioShop.Payments.Contracts.Events;
using ClarioShop.SharedKernel.Messaging;
using Microsoft.Extensions.Logging;

namespace ClarioShop.Ordering.Features.Payments;

/// <summary>
/// Keeps the order's amount paid and payment status in step with Payments; each payment counts once. A prepaid order
/// waiting for its payment is confirmed once it is paid in full, which publishes <see cref="OrderConfirmed"/> so
/// Inventory sells the stock.
/// </summary>
internal sealed partial class PaymentSucceededHandler(
    OrderingDbContext db,
    IInventoryReservations reservations,
    TimeProvider clock,
    ILogger<PaymentSucceededHandler> logger) : IIntegrationEventHandler<PaymentSucceeded>
{
    private const string InsufficientStock = "inventory.insufficient_stock";

    public async Task HandleAsync(PaymentSucceeded integrationEvent, CancellationToken cancellationToken)
    {
        var order = await db.Orders
            .Include(o => o.Payments)
            .Include(o => o.Lines)
            .Include(o => o.History)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == integrationEvent.OrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment {integrationEvent.PaymentId} is for order {integrationEvent.OrderNumber}, which does not exist.");

        var now = clock.GetUtcNow();
        if (!order.RecordPayment(integrationEvent.PaymentId, integrationEvent.Method, integrationEvent.Amount, integrationEvent.PaidAt, now))
        {
            return;
        }

        if (order.Status == OrderStatus.PendingPayment && order.PaymentStatus == OrderPaymentStatus.Paid)
        {
            await ConfirmAsync(order, now, cancellationToken);
        }
        else if (order.Status == OrderStatus.Cancelled)
        {
            // E.g. staff cancelled the order while the shopper was still on the payment page.
            LogPaidAfterCancel(logger, OrderNumber.Format(order.Number), integrationEvent.PaymentId);
        }

        // A concurrent status change (RowVersion) fails this save; the event is then redelivered and recorded on the next try.
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Confirms the paid order on the stock it holds, reserving again when the checkout hold ran out (the shopper took
    /// long on the payment page). If the stock is gone by then, the order stays PendingPayment, paid, for staff to
    /// refund or restock; the payment is still recorded.
    /// </summary>
    private async Task ConfirmAsync(Order order, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var held = await reservations.ReserveAsync(
            new ReserveStockRequest(order.Id, [.. order.Lines.Select(line => new ReservationLine(line.VariantId, line.Quantity))]),
            cancellationToken);
        if (held.IsFailure)
        {
            if (held.Error.Code != InsufficientStock)
            {
                // Let the outbox redeliver later rather than marking the payment as handled without confirming.
                throw new InvalidOperationException($"Could not hold stock for paid order {OrderNumber.Format(order.Number)}: {held.Error.Message}");
            }

            LogPaidButStockGone(logger, OrderNumber.Format(order.Number));
            return;
        }

        var confirmed = order.ConfirmPaid(held.Value.ReservationId, now);
        if (confirmed.IsFailure)
        {
            throw new InvalidOperationException(confirmed.Error.Message);
        }

        db.Publish(new OrderConfirmed(
            Guid.CreateVersion7(),
            now,
            order.Id,
            OrderNumber.Format(order.Number),
            order.CustomerId,
            order.ReservationId));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Order {OrderNumber} was paid after it was cancelled (payment {PaymentId}); refund it")]
    private static partial void LogPaidAfterCancel(ILogger logger, string orderNumber, Guid paymentId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Order {OrderNumber} is paid, but its stock hold expired and the stock is gone; it stays PendingPayment for staff to refund or restock")]
    private static partial void LogPaidButStockGone(ILogger logger, string orderNumber);
}
