using ClarioShop.Inventory.Contracts.Events;
using ClarioShop.Notifications.Domain;
using ClarioShop.Notifications.Services;
using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Notifications.Features.StockAlerts;

/// <summary>Tells the store a variant's stock went below zero, so someone counts the shelf.</summary>
internal sealed class StockWentNegativeHandler(StaffAlerter alerter) : IIntegrationEventHandler<StockWentNegative>
{
    public Task HandleAsync(StockWentNegative integrationEvent, CancellationToken cancellationToken) =>
        alerter.AlertAsync(
            new StockAlert(
                integrationEvent.EventId,
                NotificationKind.StockWentNegative,
                new Dictionary<string, string>
                {
                    [MessagePlaceholders.ProductName] = integrationEvent.ProductName,
                    [MessagePlaceholders.Sku] = integrationEvent.Sku,
                    [MessagePlaceholders.LocationCode] = integrationEvent.LocationCode,
                    [MessagePlaceholders.OnHand] = StockQuantity.Format(integrationEvent.OnHand),
                }),
            cancellationToken);
}
