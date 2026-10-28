using ClarioShop.Inventory.Services;
using ClarioShop.Ordering.Contracts.Events;
using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Inventory.Features.Orders;

/// <summary>A cancelled order's stock comes back: the hold is released, or the sale reversed if it was confirmed.</summary>
internal sealed class OrderCancelledHandler(InventoryReservations reservations) : IIntegrationEventHandler<OrderCancelled>
{
    public async Task HandleAsync(OrderCancelled integrationEvent, CancellationToken cancellationToken)
    {
        var result = await reservations.CancelOrderAsync(integrationEvent.ReservationId, cancellationToken);
        if (result.IsFailure)
        {
            // Let the outbox redeliver later rather than marking the event as handled.
            throw new InvalidOperationException($"Could not return stock for order {integrationEvent.OrderNumber}: {result.Error.Message}");
        }
    }
}
