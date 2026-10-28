using ClarioShop.Ordering.Contracts.Events;
using ClarioShop.Reporting.Domain;
using ClarioShop.Reporting.Services;
using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Reporting.Features.Projections;

/// <summary>The order was packed; it waits for the courier or rider.</summary>
internal sealed class OrderPackedHandler(SalesOrderProjection projection) : IIntegrationEventHandler<OrderPacked>
{
    public Task HandleAsync(OrderPacked integrationEvent, CancellationToken cancellationToken) =>
        projection.MoveToAsync(integrationEvent.OrderId, integrationEvent.OrderNumber, SalesOrderStatus.Packed, integrationEvent.OccurredAt, cancellationToken);
}
