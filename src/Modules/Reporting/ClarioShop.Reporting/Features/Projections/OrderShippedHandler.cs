using ClarioShop.Ordering.Contracts.Events;
using ClarioShop.Reporting.Domain;
using ClarioShop.Reporting.Services;
using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Reporting.Features.Projections;

/// <summary>The order left the shop with the courier or rider.</summary>
internal sealed class OrderShippedHandler(SalesOrderProjection projection) : IIntegrationEventHandler<OrderShipped>
{
    public Task HandleAsync(OrderShipped integrationEvent, CancellationToken cancellationToken) =>
        projection.MoveToAsync(integrationEvent.OrderId, integrationEvent.OrderNumber, SalesOrderStatus.Shipped, integrationEvent.OccurredAt, cancellationToken);
}
