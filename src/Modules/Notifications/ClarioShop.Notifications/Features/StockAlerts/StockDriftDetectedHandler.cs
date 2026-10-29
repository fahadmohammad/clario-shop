using ClarioShop.Inventory.Contracts.Events;
using ClarioShop.Notifications.Domain;
using ClarioShop.Notifications.Services;
using ClarioShop.SharedKernel.Messaging;

namespace ClarioShop.Notifications.Features.StockAlerts;

/// <summary>
/// Tells the store the drift check corrected stock levels. One check publishes an event per corrected level, but the
/// store hears once per check (keyed by the check id), with the first correction to arrive as the example; the drift
/// report lists them all.
/// </summary>
internal sealed class StockDriftDetectedHandler(StaffAlerter alerter) : IIntegrationEventHandler<StockDriftDetected>
{
    public Task HandleAsync(StockDriftDetected integrationEvent, CancellationToken cancellationToken) =>
        alerter.AlertAsync(
            new StockAlert(
                integrationEvent.CheckId,
                NotificationKind.StockDriftDetected,
                new Dictionary<string, string>
                {
                    [MessagePlaceholders.Sku] = integrationEvent.Sku,
                    [MessagePlaceholders.LocationCode] = integrationEvent.LocationCode,
                    [MessagePlaceholders.RecordedOnHand] = StockQuantity.Format(integrationEvent.RecordedOnHand),
                    [MessagePlaceholders.LedgerOnHand] = StockQuantity.Format(integrationEvent.LedgerOnHand),
                    [MessagePlaceholders.RecordedReserved] = StockQuantity.Format(integrationEvent.RecordedReserved),
                    [MessagePlaceholders.HeldReserved] = StockQuantity.Format(integrationEvent.HeldReserved),
                }),
            cancellationToken);
}
