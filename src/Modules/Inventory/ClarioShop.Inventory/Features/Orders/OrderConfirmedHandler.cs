using ClarioShop.Inventory.Domain;
using ClarioShop.Inventory.Services;
using ClarioShop.Ordering.Contracts.Events;
using ClarioShop.SharedKernel.Messaging;
using Microsoft.Extensions.Logging;

namespace ClarioShop.Inventory.Features.Orders;

/// <summary>A confirmed order's stock is sold: its reservation becomes OnlineSale movements.</summary>
internal sealed partial class OrderConfirmedHandler(InventoryReservations reservations, ILogger<OrderConfirmedHandler> logger)
    : IIntegrationEventHandler<OrderConfirmed>
{
    public async Task HandleAsync(OrderConfirmed integrationEvent, CancellationToken cancellationToken)
    {
        var result = await reservations.CommitConfirmedAsync(integrationEvent.ReservationId, cancellationToken);
        if (result.IsSuccess)
        {
            return;
        }

        if (result.Error == InventoryErrors.ReservationReleased)
        {
            // The cancellation was handled first; the stock is already back.
            LogCancelledBeforeCommit(logger, integrationEvent.OrderNumber);
            return;
        }

        // Let the outbox redeliver later rather than marking the event as handled.
        throw new InvalidOperationException($"Could not commit stock for order {integrationEvent.OrderNumber}: {result.Error.Message}");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Order {OrderNumber} was cancelled before its confirmation reached Inventory; nothing to sell")]
    private static partial void LogCancelledBeforeCommit(ILogger logger, string orderNumber);
}
