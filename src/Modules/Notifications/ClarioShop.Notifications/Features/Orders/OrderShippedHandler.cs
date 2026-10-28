using ClarioShop.Notifications.Domain;
using ClarioShop.Notifications.Services;
using ClarioShop.Ordering.Contracts.Events;
using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Notifications.Features.Orders;

/// <summary>Tells the shopper their order is on its way.</summary>
internal sealed class OrderShippedHandler(OrderNotifier notifier) : IIntegrationEventHandler<OrderShipped>
{
    public Task HandleAsync(OrderShipped integrationEvent, CancellationToken cancellationToken) =>
        notifier.NotifyAsync(
            new OrderNotification(
                integrationEvent.EventId,
                OrderNotificationKind.OrderShipped,
                integrationEvent.OrderId,
                integrationEvent.OrderNumber,
                integrationEvent.CustomerId),
            cancellationToken);
}
