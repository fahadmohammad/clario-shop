using ClarioShop.Ordering.Contracts.Events;
using ClarioShop.Reporting.Domain;
using ClarioShop.Reporting.Services;
using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Reporting.Features.Projections;

/// <summary>The order was cancelled; it no longer counts as a sale.</summary>
internal sealed class OrderCancelledHandler(SalesOrderProjection projection) : IIntegrationEventHandler<OrderCancelled>
{
    public Task HandleAsync(OrderCancelled integrationEvent, CancellationToken cancellationToken) =>
        projection.MoveToAsync(integrationEvent.OrderId, integrationEvent.OrderNumber, SalesOrderStatus.Cancelled, integrationEvent.OccurredAt, cancellationToken);
}
