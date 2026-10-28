using ClarioShop.Ordering.Contracts.Events;
using ClarioShop.Reporting.Domain;
using ClarioShop.Reporting.Services;
using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Reporting.Features.Projections;

/// <summary>The order reached the shopper.</summary>
internal sealed class OrderDeliveredHandler(SalesOrderProjection projection) : IIntegrationEventHandler<OrderDelivered>
{
    public Task HandleAsync(OrderDelivered integrationEvent, CancellationToken cancellationToken) =>
        projection.MoveToAsync(integrationEvent.OrderId, integrationEvent.OrderNumber, SalesOrderStatus.Delivered, integrationEvent.OccurredAt, cancellationToken);
}
