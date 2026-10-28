using ClarioShop.Notifications.Domain;
using ClarioShop.Notifications.Services;
using ClarioShop.Ordering.Contracts.Events;
using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Notifications.Features.Orders;

/// <summary>Thanks the shopper once their order is delivered.</summary>
internal sealed class OrderDeliveredHandler(OrderNotifier notifier) : IIntegrationEventHandler<OrderDelivered>
{
    public Task HandleAsync(OrderDelivered integrationEvent, CancellationToken cancellationToken) =>
        notifier.NotifyAsync(
            new OrderNotification(
                integrationEvent.EventId,
                OrderNotificationKind.OrderDelivered,
                integrationEvent.OrderId,
                integrationEvent.OrderNumber,
                integrationEvent.CustomerId),
            cancellationToken);
}
