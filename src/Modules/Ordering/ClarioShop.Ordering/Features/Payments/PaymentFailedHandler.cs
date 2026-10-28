using ClarioShop.Ordering.Contracts.Events;
using ClarioShop.Ordering.Domain;
using ClarioShop.Ordering.Features.Orders;
using ClarioShop.Ordering.Persistence;
using ClarioShop.Payments.Contracts.Events;
using ClarioShop.SharedKernel.Messaging;
using Microsoft.Extensions.Logging;

namespace ClarioShop.Ordering.Features.Payments;

/// <summary>
/// Cancels a prepaid order whose payment failed, publishing <see cref="OrderCancelled"/> so Inventory releases its
/// stock. An order that has moved on (cancelled by staff, or paid another way) is left as it is.
/// </summary>
internal sealed partial class PaymentFailedHandler(
    OrderingDbContext db,
    TimeProvider clock,
    ILogger<PaymentFailedHandler> logger) : IIntegrationEventHandler<PaymentFailed>
{
    public async Task HandleAsync(PaymentFailed integrationEvent, CancellationToken cancellationToken)
    {
        var order = await db.LoadOrderAsync(integrationEvent.OrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment {integrationEvent.PaymentId} is for order {integrationEvent.OrderNumber}, which does not exist.");

        var previous = order.Status;
        var now = clock.GetUtcNow();
        var cancelled = order.CancelUnpaid(integrationEvent.Reason, now);
        if (cancelled.IsFailure)
        {
            LogNotCancelled(logger, OrderNumber.Format(order.Number), integrationEvent.PaymentId, cancelled.Error.Message);
            return;
        }

        db.Publish(new OrderCancelled(
            Guid.CreateVersion7(),
            now,
            order.Id,
            OrderNumber.Format(order.Number),
            order.CustomerId,
            order.ReservationId,
            previous.ToString(),
            "Payment failed"));

        // A concurrent status change (RowVersion) fails this save; the event is then redelivered and looked at again.
        await db.SaveChangesAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Order {OrderNumber} is not cancelled for its failed payment {PaymentId}: {Reason}")]
    private static partial void LogNotCancelled(ILogger logger, string orderNumber, Guid paymentId, string reason);
}
