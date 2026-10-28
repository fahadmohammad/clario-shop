using ClarioShop.Ordering.Persistence;
using ClarioShop.Payments.Contracts.Events;
using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Ordering.Features.Payments;

/// <summary>Keeps the order's amount paid and payment status in step with Payments. Each payment counts once.</summary>
internal sealed class PaymentSucceededHandler(OrderingDbContext db, TimeProvider clock) : IIntegrationEventHandler<PaymentSucceeded>
{
    public async Task HandleAsync(PaymentSucceeded integrationEvent, CancellationToken cancellationToken)
    {
        var order = await db.Orders
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == integrationEvent.OrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Payment {integrationEvent.PaymentId} is for order {integrationEvent.OrderNumber}, which does not exist.");

        if (order.RecordPayment(integrationEvent.PaymentId, integrationEvent.Method, integrationEvent.Amount, integrationEvent.PaidAt, clock.GetUtcNow()))
        {
            // A concurrent status change (RowVersion) fails this save; the event is then redelivered and recorded on the next try.
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
