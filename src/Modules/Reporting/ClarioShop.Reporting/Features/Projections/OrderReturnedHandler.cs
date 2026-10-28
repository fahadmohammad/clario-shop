using ClarioShop.Ordering.Contracts.Events;
using ClarioShop.Reporting.Domain;
using ClarioShop.Reporting.Services;
using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Reporting.Features.Projections;

/// <summary>The order came back; it no longer counts as a sale.</summary>
internal sealed class OrderReturnedHandler(SalesOrderProjection projection) : IIntegrationEventHandler<OrderReturned>
{
    public Task HandleAsync(OrderReturned integrationEvent, CancellationToken cancellationToken) =>
        projection.MoveToAsync(integrationEvent.OrderId, integrationEvent.OrderNumber, SalesOrderStatus.Returned, integrationEvent.OccurredAt, cancellationToken);
}
