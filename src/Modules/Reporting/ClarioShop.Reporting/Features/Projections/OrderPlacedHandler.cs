using ClarioShop.Ordering.Contracts;
using ClarioShop.Ordering.Contracts.Events;
using ClarioShop.Reporting.Domain;
using ClarioShop.Reporting.Services;
using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Reporting.Features.Projections;

/// <summary>Records what the order is worth and what is on it; it counts as a sale only once confirmed.</summary>
internal sealed class OrderPlacedHandler(SalesOrderProjection projection, StoreCalendar calendar) : IIntegrationEventHandler<OrderPlaced>
{
    public Task HandleAsync(OrderPlaced integrationEvent, CancellationToken cancellationToken)
    {
        var status = integrationEvent.PaymentMethod == OrderPaymentMethods.CashOnDelivery ? SalesOrderStatus.Placed : SalesOrderStatus.PendingPayment;
        var placement = new SalesOrderPlacement(
            status,
            integrationEvent.PaymentMethod,
            integrationEvent.Subtotal,
            integrationEvent.DeliveryFee,
            integrationEvent.Vat,
            integrationEvent.Total,
            integrationEvent.OccurredAt,
            calendar.DayOf(integrationEvent.OccurredAt),
            [.. integrationEvent.Lines.Select(line => new SalesOrderLineSpec(
                line.VariantId, line.Sku, line.ProductName, line.VariantName, line.Quantity, line.UnitPrice, line.LineTotal))]);

        return projection.ApplyAsync(
            integrationEvent.OrderId,
            () => SalesOrder.Start(integrationEvent.OrderId, integrationEvent.OrderNumber, status, integrationEvent.OccurredAt),
            order => order.RecordPlaced(placement),
            cancellationToken);
    }
}
