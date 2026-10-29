using ClarioShop.Notifications.Domain;
using ClarioShop.Notifications.Services;
using ClarioShop.Ordering.Contracts.Events;
using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Notifications.Features.Orders;

/// <summary>Tells the shopper their order is confirmed (by staff, or once a prepaid order is paid).</summary>
internal sealed class OrderConfirmedHandler(OrderNotifier notifier) : IIntegrationEventHandler<OrderConfirmed>
{
    public Task HandleAsync(OrderConfirmed integrationEvent, CancellationToken cancellationToken) =>
        notifier.NotifyAsync(
            new OrderNotification(
                integrationEvent.EventId,
                NotificationKind.OrderConfirmed,
                integrationEvent.OrderId,
                integrationEvent.OrderNumber,
                integrationEvent.CustomerId),
            cancellationToken);
}
