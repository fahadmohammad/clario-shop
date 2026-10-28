using ClarioShop.Ordering.Contracts.Events;
using ClarioShop.Reporting.Domain;
using ClarioShop.Reporting.Services;
using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Reporting.Features.Projections;

/// <summary>The order's stock is sold: from now on it counts as a sale, on the store day it was confirmed.</summary>
internal sealed class OrderConfirmedHandler(SalesOrderProjection projection, StoreCalendar calendar) : IIntegrationEventHandler<OrderConfirmed>
{
    public Task HandleAsync(OrderConfirmed integrationEvent, CancellationToken cancellationToken) =>
        projection.ApplyAsync(
            integrationEvent.OrderId,
            () => SalesOrder.Start(integrationEvent.OrderId, integrationEvent.OrderNumber, SalesOrderStatus.Confirmed, integrationEvent.OccurredAt),
            order => order.RecordConfirmed(integrationEvent.OccurredAt, calendar.DayOf(integrationEvent.OccurredAt)),
            cancellationToken);
}
