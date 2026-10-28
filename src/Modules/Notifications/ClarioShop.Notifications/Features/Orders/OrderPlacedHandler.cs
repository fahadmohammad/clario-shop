using ClarioShop.Notifications.Domain;
using ClarioShop.Notifications.Services;
using ClarioShop.Ordering.Contracts;
using ClarioShop.Ordering.Contracts.Events;
using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Notifications.Features.Orders;

/// <summary>
/// Thanks the shopper for a cash-on-delivery order. A prepaid order is not announced yet: the shopper is on the
/// payment page, and hears from the store once it is paid and confirmed.
/// </summary>
internal sealed class OrderPlacedHandler(OrderNotifier notifier) : IIntegrationEventHandler<OrderPlaced>
{
    public Task HandleAsync(OrderPlaced integrationEvent, CancellationToken cancellationToken) =>
        integrationEvent.PaymentMethod == OrderPaymentMethods.CashOnDelivery
            ? notifier.NotifyAsync(
                new OrderNotification(
                    integrationEvent.EventId,
                    OrderNotificationKind.OrderPlaced,
                    integrationEvent.OrderId,
                    integrationEvent.OrderNumber,
                    integrationEvent.CustomerId,
                    integrationEvent.Total),
                cancellationToken)
            : Task.CompletedTask;
}
